using RadioExpansion.Core.WaveStreams;
using NAudio.Wave;
using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using RadioExpansion.Core.Logging;
using RadioExpansion.Core.Serialization;

namespace RadioExpansion.Core.RadioPlayers
{
    [XmlWhitelistSerialization]
    public class LocalRadio : Radio
    {
        private DateTime _createdAt;
        private TimeSpan _initialPosition;
        private double _totalTimeInSeconds;
        private Mp3FileReader _mp3Stream;
        private string _filePath;

        private static Random _rndStreamStart = new Random();

        protected override int MetaDataSyncInterval => 3000;

        [XmlWhitelisted, XmlArrayItem("Track")]
        public LocalStreamMetaData[] TrackList { get; set; }

        protected override void OnPathChanged()
        {
            var mp3Files = Directory.GetFiles(AbsoluteDirectoryPath).Where(f => Path.GetExtension(f).ToLower() == ".mp3").ToArray();

            if (mp3Files.Length > 0)
            {
                _filePath = mp3Files[0];

                if (mp3Files.Length > 1)
                {
                    Logger.Log($"{mp3Files.Length} MP3 files were found in local radio folder '{RelativeDirectoryPath}'. Only one file is allowed! Took '{_filePath}', ignored the rest.");
                }
                _createdAt = DateTime.Now;

                using (var mp3Stream = new Mp3FileReader(_filePath))
                {
                    _totalTimeInSeconds = mp3Stream.TotalTime.TotalSeconds;
                }

                _initialPosition = TimeSpan.FromSeconds(_rndStreamStart.NextDouble() * _totalTimeInSeconds); // don't start the audio always from beginning!
            }
            else
            {
                Logger.Log($"No MP3 files were found in local radio folder '{RelativeDirectoryPath}'");
            }
        }

        public override void RefreshMetaInfo()
        {
            if (TrackList != null)
            {
                var currentPosition = TimeSpan.FromSeconds(CalculatePosition());
                CurrentTrackMetaData = TrackList.FirstOrDefault(m => m.Start <= currentPosition && currentPosition <= m.End);
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
