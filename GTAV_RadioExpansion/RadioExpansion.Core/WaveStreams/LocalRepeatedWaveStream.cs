using NAudio.Wave;
using System.IO;

namespace RadioExpansion.Core.WaveStreams
{
    /// <summary>
    /// Wrapper for single local audio streams, which restarts at the end.
    /// </summary>
    public class LocalRepeatedWaveStream : WaveStream
    {
        private readonly WaveStream _sourceStream;
        
        public override long Length => _sourceStream.Length;

        public override WaveFormat WaveFormat => _sourceStream.WaveFormat;

        public override long Position
        {
            get { return _sourceStream.Position; }
            set { _sourceStream.Position = value; }
        }

        public LocalRepeatedWaveStream(WaveStream sourceStream)
        {
            _sourceStream = sourceStream;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = _sourceStream.Read(buffer, offset, count);

            if (_sourceStream.Position == _sourceStream.Length) // end of stream reached
            {
                _sourceStream.Seek(0, SeekOrigin.Begin); // start it from the beginning
            }

            return bytesRead;
        }
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            
            _sourceStream.Dispose();
        }
    }
}
