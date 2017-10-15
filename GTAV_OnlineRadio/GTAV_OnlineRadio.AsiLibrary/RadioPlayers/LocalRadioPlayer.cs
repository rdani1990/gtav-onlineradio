using GTAV_OnlineRadio.AsiLibrary.WaveStreams;
using NAudio.Wave;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace GTAV_OnlineRadio.AsiLibrary.RadioPlayers
{
    public class LocalRadioPlayer : Radio
    {
        private DateTime _createdAt;
        private TimeSpan _initialPosition;
        private double _totalTimeInSeconds;
        private Mp3FileReader _mp3Stream;
        private LocalStreamMetaData[] _metaData;

        private static Random _rndStreamStart = new Random();
        
        private const int META_SYNC_INTERVAL = 3000;

        private string _filePath;

        //public LocalRadioPlayer(string name, Uri uri, float volume, XElement metaData) : base(name, uri, volume, META_SYNC_INTERVAL)
        //{
        //    _createdAt = DateTime.Now;
            
        //    using (var mp3Stream = new Mp3FileReader(_uri.LocalPath))
        //    {
        //        _totalTimeInSeconds = mp3Stream.TotalTime.TotalSeconds;
        //    }

        //    _initialPosition = TimeSpan.FromSeconds(_rndStreamStart.NextDouble() * _totalTimeInSeconds); // don't start the audio always from beginning!

        //    if (metaData != null)
        //    {
        //        _metaData = metaData.Elements().Select(mi => new LocalStreamMetaData(mi)).ToArray();
        //    }
        //}

        public LocalRadioPlayer(string filePath, XElement config) : base(Path.GetDirectoryName(filePath), config, META_SYNC_INTERVAL)
        {
            _filePath = filePath;
            _createdAt = DateTime.Now;

            using (var mp3Stream = new Mp3FileReader(_filePath))
            {
                _totalTimeInSeconds = mp3Stream.TotalTime.TotalSeconds;
            }

            _initialPosition = TimeSpan.FromSeconds(_rndStreamStart.NextDouble() * _totalTimeInSeconds); // don't start the audio always from beginning!

            _metaData = config?.Element("TrackList").Elements().Select(mi => new LocalStreamMetaData(mi)).ToArray();
        }

        public override void RefreshMetaInfo()
        {
            if (_metaData != null)
            {
                var currentPosition = TimeSpan.FromSeconds(CalculatePosition());
                CurrentTrackMetaData = _metaData.FirstOrDefault(m => m.Start <= currentPosition && currentPosition <= m.End);
            }
        }

        protected override void StreamAudio()
        {
            _mp3Stream = new Mp3FileReader(_filePath);
            _mp3Stream.CurrentTime = TimeSpan.FromSeconds(CalculatePosition());

            StreamAudio(new LocalRepeatedWaveStream(_mp3Stream));
        }

        private double CalculatePosition()
        {
            var elapsedTimeSinceStart = DateTime.Now - _createdAt; // time elapsed since radio created
            return (_initialPosition.TotalSeconds + elapsedTimeSinceStart.TotalSeconds) % _totalTimeInSeconds; // if the stream wouldn't be stopped, and loop is on, where would it be now?
        }
        
    }
}
