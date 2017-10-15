using NAudio.Wave;
using System;

namespace GTAV_OnlineRadio.AsiLibrary.WaveStreams
{
    /// <summary>
    /// Wrapper for local audio streams, supports source stream changing.
    /// </summary>
    public class LocalClusteredWaveStream : WaveStream
    {
        private static readonly WaveFormat _defaultWaveFormat = new WaveFormat();

        private IWaveProvider _sourceProvider;
        private WaveStream _sourceStream;

        public override long Length => _sourceStream.Length;

        public override WaveFormat WaveFormat => _sourceProvider.WaveFormat;
        
        public WaveStream SourceStream
        {
            get
            {
                return _sourceStream;
            }
            set
            {
                if (_sourceStream != null)
                {
                    _sourceStream.Close(); // close previous stream
                }

                if (!WaveStreamHelper.IsWaveFormatEqual(value.WaveFormat, _defaultWaveFormat)) // if new waveformat differs from PCM 44.1 kHz stereo, then we need to resample the stream
                {
                    _sourceProvider = new MediaFoundationResampler(value, _defaultWaveFormat);
                }
                else
                {
                    _sourceProvider = value; // otherwise the provider is the stream itself
                }
                
                _sourceStream = value;
            }
        }

        public override TimeSpan TotalTime => _sourceStream.TotalTime;

        public override TimeSpan CurrentTime
        {
            get { return _sourceStream.CurrentTime; }
            set { _sourceStream.CurrentTime = value; }
        }

        public event EventHandler EndOfStreamReached;

        public override long Position
        {
            get
            {
                return _sourceStream.Position;
            }
            set
            {
                _sourceStream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = _sourceProvider.Read(buffer, offset, count);
            if (bytesRead == 0 && _sourceStream.CurrentTime.TotalMilliseconds > (_sourceStream.TotalTime.TotalMilliseconds * 0.99)) // end of current stream reached, shout to switch the stream. The 1 % needed for FLAC streams which's end could be read, but for some reason doesn't
            {
                EndOfStreamReached?.Invoke(this, EventArgs.Empty);
            }
            return bytesRead;
        }
        
        protected override void Dispose(bool disposing)
        {
            /*base.Dispose(disposing);

            _sourceStream.Dispose();*/
        }
    }
}