using GTAV_OnlineRadio.AsiLibrary.WaveStreams;
using IWshRuntimeLibrary;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using File = System.IO.File;

namespace GTAV_OnlineRadio.AsiLibrary.RadioPlayers
{
    /// <summary>
    /// Same conception what San Andreas radio stations had: instead of having one big file, the adverts, intro and outro parts, and track bodies are in separated files.
    /// </summary>
    public class ClusteredRadio : Radio
    {
        private LocalClusteredWaveStream _audioStream;
        private Queue<ClusteredTrack> _musicTracks;
        private Queue<string> _adverts;

        private CurrentTrackInfo _currentTrackInfo;
        private bool _metaLoaded;
        private object _moveToNextTrackLock = new object();
        
        private const int META_SYNC_INTERVAL = 3000;

        private static Random _rnd = new Random();

        public override float BufferLengthInSeconds => 5;

        public int TrackCount => _musicTracks.Count;

        public int AdvertCount => _adverts.Count;

        //public ClusteredRadio(string name, Uri uri, float volume, XElement trackList, XElement adverts) : base(name, uri, volume, META_SYNC_INTERVAL)
        //{
        //    _musicTracks = new Queue<ClusteredTrack>();
        //    _pathAdverts = new Queue<string>();
        //    _audioStream = new LocalClusteredWaveStream();
        //    _audioStream.EndOfStreamReached += AudioStream_EndOfStreamReached;

        //    var musicTracks = trackList.Elements("Track").Select(t => new ClusteredTrack(t)).ToList();
        //    var pathAdverts = adverts.Elements("Advert").Select(t => t.Value).ToList();

        //    while (musicTracks.Count > 0)
        //    {
        //        int trackIndex = _rnd.Next(musicTracks.Count);
        //        _musicTracks.Enqueue(musicTracks[trackIndex]);
        //        musicTracks.RemoveAt(trackIndex);
        //    }

        //    while (pathAdverts.Count > 0)
        //    {
        //        int advertIndex = _rnd.Next(pathAdverts.Count);
        //        _pathAdverts.Enqueue(pathAdverts[advertIndex]);
        //        pathAdverts.RemoveAt(advertIndex);
        //    }

        //    _metaLoaded = true;
        //}

        public ClusteredRadio(string folder, IEnumerable<string> files, XElement config) : base(folder, config, META_SYNC_INTERVAL)
        {
            _musicTracks = new Queue<ClusteredTrack>();
            _adverts = new Queue<string>();
            _audioStream = new LocalClusteredWaveStream();

            var musicTracks = new List<ClusteredTrack>();
            var adverts = new List<string>();

            foreach (string path in files)
            {
                if (File.Exists(path))
                {
                    var track = musicTracks.FirstOrDefault(t => t.IsFilePartOfThisTrack(path));

                    try
                    {
                        if (track == null)
                        {
                            musicTracks.Add(new ClusteredTrack(path));
                        }
                        else
                        {
                            track.SetPath(path);
                        }
                    }
                    catch (FileNotFoundException ex)
                    {
                        Logger.Instance.Log(ex.Message);
                    }
                }
                else
                {
                    Logger.Instance.Log($"Skipping file '{path}'. File not found");
                }
            }

            if (Directory.Exists(Path.Combine(folder, "[adverts]"))) // custom adverts for the radio station
            {
                adverts.AddRange(Directory.GetFiles(Path.Combine(folder, "[adverts]")));
            }
            if (Directory.Exists(Path.Combine(folder, "..\\[adverts]"))) // generic adverts
            {
                adverts.AddRange(Directory.GetFiles(Path.Combine(folder, "..\\[adverts]")));
            }

            adverts = adverts.Where(p => HasAllowedAudioExtension(p)).ToList();

            // randomize the order of the tracks
            while (musicTracks.Count > 0)
            {
                int trackIndex = _rnd.Next(musicTracks.Count);
                _musicTracks.Enqueue(musicTracks[trackIndex]);
                musicTracks.RemoveAt(trackIndex);
            }

            while (adverts.Count > 0)
            {
                int advertIndex = _rnd.Next(adverts.Count);
                _adverts.Enqueue(Path.GetFullPath(adverts[advertIndex]));
                adverts.RemoveAt(advertIndex);
            }

            _audioStream.EndOfStreamReached += AudioStream_EndOfStreamReached;

            _metaLoaded = true;
        }

        public override void RefreshMetaInfo()
        {
            if (_metaLoaded)
            {
                bool isFirstTrack = (_currentTrackInfo == null);
                if ((isFirstTrack || _currentTrackInfo.StartedPlayingAt.Add(_currentTrackInfo.TotalTime) < DateTime.Now) && Monitor.TryEnter(_moveToNextTrackLock))
                {
                    var nextTrack = MoveToNextTrack();

                    if (nextTrack != null)
                    {
                        _audioStream.SourceStream = MoveToNextTrack();

                        if (isFirstTrack)
                        {
                            _audioStream.CurrentTime = TimeSpan.FromSeconds(_currentTrackInfo.TotalTime.TotalSeconds * _rnd.NextDouble());
                            _currentTrackInfo.StartedPlayingAt = _currentTrackInfo.StartedPlayingAt.AddSeconds(-1 * _audioStream.CurrentTime.TotalSeconds);
                        }
                    }
                    else
                    {
                        _metaSyncTimer.Enabled = false; // if no audio music was found, it means, that it's doesn't mnake any sense to check for metainfo change
                    }
                    
                    Monitor.Exit(_moveToNextTrackLock);
                }

                if (_currentTrackInfo != null)
                {
                    CurrentTrackMetaData = new MetaData(_currentTrackInfo.MetaData?.Artist, _currentTrackInfo.MetaData?.Track);
                }
            }
        }

        protected override void StreamAudio()
        {
            var now = DateTime.Now;
            if ((_currentTrackInfo == null || _currentTrackInfo.StartedPlayingAt.Add(_currentTrackInfo.TotalTime) < now) && Monitor.TryEnter(_moveToNextTrackLock))
            {
                _audioStream.SourceStream = MoveToNextTrack();
                _audioStream.CurrentTime = TimeSpan.FromSeconds(_currentTrackInfo.TotalTime.TotalSeconds * _rnd.NextDouble());

                Monitor.Exit(_moveToNextTrackLock);
            }
            else
            {
                _audioStream.CurrentTime = now - _currentTrackInfo.StartedPlayingAt;
            }

            StreamAudio(_audioStream);
        }
        
        private void AudioStream_EndOfStreamReached(object sender, EventArgs e)
        {
            _audioStream.SourceStream = MoveToNextTrack();
        }

        /// <summary>
        /// If the currently played track is finished, drops it to the end of the playing queue.
        /// </summary>
        private void DropFinishedTrackToQueueEnd()
        {
            if (_currentTrackInfo?.IsAdvert == true)
            {
                _adverts.Enqueue(_adverts.Dequeue());
            }
            else if (_currentTrackInfo?.IsAdvert == false)
            {
                _musicTracks.Enqueue(_musicTracks.Dequeue());
            }
        }

        /// <summary>
        /// If a music's intro or mid was playing, returns its next part.
        /// </summary>
        private string GetNextTrackForOngoingMusic()
        {
            if (_currentTrackInfo?.IsAdvert == false) // music is played
            {
                var firstTrackInList = _musicTracks.Peek();

                if (firstTrackInList.Intros.Contains(_currentTrackInfo.Path)) // intro was playing?
                {
                    return firstTrackInList.Mid; // get the middle part of the music
                }
                else if (firstTrackInList.Mid == _currentTrackInfo.Path && firstTrackInList.Outros.Count > 0) // if a mid got to its end, then get an outro, if there's any
                {
                    return firstTrackInList.Outros[_rnd.Next(firstTrackInList.Outros.Count)];
                }
            }
            
            return null;
        }

        private WaveStream MoveToNextTrack()
        {
            var newTrackInfo = new CurrentTrackInfo();
            string nextTrackToOpen = null;

            DropFinishedTrackToQueueEnd();
            
            while (_musicTracks.Count > 0 || _adverts.Count > 0) // until there's something to play
            {
                try
                {
                    WaveStream newStream = null;

                    if (_musicTracks.Count > 0 && (_rnd.Next(100) < 60 || _adverts.Count == 0))
                    {
                        var newTrack = _musicTracks.Peek();
                        string intro = (newTrack.Intros.Count > 0 ? newTrack.Intros[_rnd.Next(newTrack.Intros.Count)] : null);
                        string outro = (newTrack.Outros.Count > 0 ? newTrack.Outros[_rnd.Next(newTrack.Outros.Count)] : null);

                        newTrackInfo.IsAdvert = false;

                        nextTrackToOpen = intro;
                        var introStream = WaveStreamHelper.OpenAudioFile(intro);

                        nextTrackToOpen = newTrack.Mid;
                        var midStream = WaveStreamHelper.OpenAudioFile(newTrack.Mid);

                        nextTrackToOpen = outro;
                        var outroStream = WaveStreamHelper.OpenAudioFile(outro);

                        newStream = new LocalConcatenatedWaveStream(introStream, midStream, outroStream);

                        newTrackInfo.MetaData = WaveStreamHelper.GetAudioMetaData(newTrack.Mid);
                    }
                    else  if (_adverts.Count > 0) // if still nothing for next track (which means we've reached the end of the currently played music, or we haven't played anything yet), but we have adverts, give it 30 % chance to play one
                    {
                        newTrackInfo.IsAdvert = true;
                        nextTrackToOpen = _adverts.Peek();
                        newStream = WaveStreamHelper.OpenAudioFile(nextTrackToOpen);
                    }
                    else
                    {
                        continue;
                    }
                    
                    newTrackInfo.TotalTime = newStream.TotalTime;
                    newTrackInfo.StartedPlayingAt = DateTime.Now;

                    _currentTrackInfo = newTrackInfo;

                    return newStream;
                }
                catch (Exception ex)
                {
                    // the next track is either not supported, not found, corrupted, or whoever knows, but we've failed to open, so we drop it from the queue
                    if (newTrackInfo.IsAdvert)
                    {
                        _adverts.Dequeue();
                    }
                    else
                    {
                        _musicTracks.Dequeue();
                    }

                    Logger.Instance.Log("Failed to open track '{0}' for radio '{1}', error:\r\n{2}", nextTrackToOpen, Name, ex);
                }
            }

            Logger.Instance.Log($"Failed to find any track to open for radio '{Name}'");
            return null;
        }

    }

    public class ClusteredTrack
    {
        public List<string> Intros { get; set; }
        public string Mid { get; set; }
        public List<string> Outros { get; set; }
        
        public ClusteredTrack(XElement track)
        {
            Intros = new List<string>(track.Descendants("Intro").Select(e => e.Value));
            Outros = new List<string>(track.Descendants("Outro").Select(e => e.Value));
            Mid = track.Element("Mid").Value;
        }

        public ClusteredTrack()
        {
            Intros = new List<string>();
            Outros = new List<string>();
        }

        public ClusteredTrack(string path) : this()
        {
            SetPath(path);
        }

        public void SetPath(string path)
        {
            if (Path.GetExtension(path).ToLower() == ".lnk") // for shortcuts, resolve the real path
            {
                var shell = new WshShell();
                var link = (IWshShortcut)shell.CreateShortcut(path);

                if (!System.IO.File.Exists(link.TargetPath))
                {
                    throw new FileNotFoundException($"Shortcut '{path}' links to {link.TargetPath}, but the file doesn't exists.");
                }

                path = link.TargetPath;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);

            if (Regex.IsMatch(fileName, "\\(Intro.*?\\)$"))
            {
                Intros.Add(path);
            }
            else if (Regex.IsMatch(fileName, "\\(Outro.*?\\)$"))
            {
                Outros.Add(path);
            }
            else
            {
                Mid = path;
            }
        }

        public override string ToString()
        {
            return Mid;
        }

        /// <summary>
        /// The filename fits in the pattern of the track's other parts.
        /// </summary>
        public bool IsFilePartOfThisTrack(string path)
        {
            string trackPattern = "\\((Intro|Mid|Outro).*?\\)$";
            string fileName = Regex.Replace(Path.GetFileNameWithoutExtension(path), trackPattern, "");

            return (Intros.Any(x => fileName == Regex.Replace(Path.GetFileNameWithoutExtension(x), trackPattern, "")) ||
                    Outros.Any(x => fileName == Regex.Replace(Path.GetFileNameWithoutExtension(x), trackPattern, "")) ||
                    ((Mid != null && fileName == Regex.Replace(Path.GetFileNameWithoutExtension(Mid), trackPattern, ""))));
        }
    }

    public class CurrentTrackInfo
    {
        public string Path { get; set; }
        public bool IsAdvert { get; set; }
        public TimeSpan TotalTime { get; set; }
        public DateTime StartedPlayingAt { get; set; }
        public MetaData MetaData { get; set; }
    }
}
