using NAudio.Wave;
using System;
using System.IO;

namespace RadioExpansion.Core.WaveStreams
{
    /// <summary>
    /// Wrapper for single local audio streams, which restarts at the end.
    /// </summary>
    public class ShoutcastWaveStream : WaveStream
    {
        private readonly BufferedShoutcastStream _sourceStream;
        private IMp3FrameDecompressor decompressor;

        public override long Length => _sourceStream.Length;

        public override WaveFormat WaveFormat => decompressor.OutputFormat;

        public override long Position
        {
            get { return _sourceStream.Position; }
            set { _sourceStream.Position = value; }
        }

        public ShoutcastWaveStream(BufferedShoutcastStream sourceStream, Mp3WaveFormat waveFormat)
        {
            _sourceStream = sourceStream;
            decompressor = new AcmMp3FrameDecompressor(waveFormat);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var frame = Mp3Frame.LoadFromStream(_sourceStream);

            if (frame != null)
            {
                return decompressor.DecompressFrame(frame, buffer, 0);
            }
            else
            {
                return 0;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (decompressor != null)
            {
                decompressor.Dispose();
            }

            _sourceStream.Dispose();
        }
    }

    public class BufferedShoutcastStream : Stream
    {
        private const int BUFFER_SIZE = 4096;

        private readonly Stream sourceStream;
        private long pos; // psuedo-position
        private readonly byte[] readAheadBuffer;
        private int readAheadLength;
        private int readAheadOffset;

        private int _receivedBytesUntilIcyMetaInt; // number of bytes read before the next meta info

        public event EventHandler<MetaDataReceivedEventArgs> MetaDataReceived;

        public int? IcyMetaInt { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => sourceStream.CanSeek;

        public override bool CanWrite => false;

        public override long Length => pos;

        public override long Position
        {
            get
            {
                return pos;
            }
            set
            {
                //throw new InvalidOperationException();
            }
        }

        public BufferedShoutcastStream(Stream sourceStream, int? icyMetaInt)
        {
            this.sourceStream = sourceStream;
            readAheadBuffer = new byte[BUFFER_SIZE];

            IcyMetaInt = icyMetaInt;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;

            while (bytesRead < count)
            {
                int readAheadAvailableBytes = readAheadLength - readAheadOffset; // number of bytes in the buffer that weren't copied already
                int bytesRequired = count - bytesRead;
                if (readAheadAvailableBytes > 0) // having buffered bytes? Then copy from the buffer
                {
                    int toCopy = Math.Min(readAheadAvailableBytes, bytesRequired);
                    Array.Copy(readAheadBuffer, readAheadOffset, buffer, offset + bytesRead, toCopy);
                    bytesRead += toCopy;
                    readAheadOffset += toCopy;
                }
                else if (!sourceStream.CanRead) // no buffered bytes, but stream is closed? Quit.
                {
                    break;
                }
                else // no buffered bytes? Read from the stream to the buffer, and start again
                {
                    if (_receivedBytesUntilIcyMetaInt == IcyMetaInt) // after every x bytes (x specified by IcyMetaInt) comes the metadata info
                    {
                        var _currentTrackMetaData = MetaHelper.ReadMetaData(sourceStream);

                        if (_currentTrackMetaData != null)
                        {
                            MetaDataReceived?.Invoke(this, new MetaDataReceivedEventArgs(_currentTrackMetaData));
                        }

                        _receivedBytesUntilIcyMetaInt = 0;
                    }

                    readAheadOffset = 0;

                    if (IcyMetaInt.HasValue)
                    {
                        readAheadLength = sourceStream.Read(readAheadBuffer, 0, Math.Min(BUFFER_SIZE, IcyMetaInt.Value - _receivedBytesUntilIcyMetaInt));
                        _receivedBytesUntilIcyMetaInt += readAheadLength;
                    }
                    else
                    {
                        readAheadLength = sourceStream.Read(readAheadBuffer, 0, BUFFER_SIZE);
                    }

                    if (readAheadLength == 0)
                    {
                        break; // there's no point to jump in the cycle again, if no bytes were read from the stream
                    }
                }
            }

            pos += bytesRead;
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        public override void Flush()
        {
            throw new InvalidOperationException();
        }

    }
}
