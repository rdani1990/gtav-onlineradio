using NAudio.Wave;
using System;

namespace GTAV_OnlineRadio.AsiLibrary.WaveStreams
{
    /// <summary>
    /// Concatenates an intro, a mid, and an outro wave stream into one whole stream. It's necessary, since resampling can cause ticking at parts where the streams are switched
    /// </summary>
    public class LocalConcatenatedWaveStream : WaveStream
    {
        private WaveStream _intro;
        private WaveStream _mid;
        private WaveStream _outro;

        private bool _introFinished;
        private bool _midFinished;

        public WaveStream OngoingStream
        {
            get
            {
                if (_mid.Position == _mid.Length || _midFinished)
                {
                    return _outro ?? _mid;
                }
                else if(_intro?.Position == _intro?.Length || _introFinished)
                {
                    return _mid;
                }
                else
                {
                    return _intro ?? _mid;
                }
            }
        }

        public LocalConcatenatedWaveStream(WaveStream intro, WaveStream mid, WaveStream outro)
        {
            if (mid == null)
            {
                throw new ArgumentNullException("Middle of the stream cannot be null!");
            }
            else if ((intro != null && !WaveStreamHelper.IsWaveFormatEqual(intro.WaveFormat, mid.WaveFormat)) ||
                     (outro != null && !WaveStreamHelper.IsWaveFormatEqual(outro.WaveFormat, mid.WaveFormat)))
            {
                throw new ArgumentException("The parts to be concatenated have to use the same format!");
            }

            _intro = intro;
            _mid = mid;
            _outro = outro;
        }

        public override long Length => (_intro?.Length ?? 0) + _mid.Length + (_outro?.Length ?? 0);

        public override WaveFormat WaveFormat => _mid.WaveFormat;
        
        public override long Position
        {
            get
            {
                return ((_introFinished ? _intro?.Length : _intro?.Position) ?? 0) + (_midFinished ? _mid.Length : _mid.Position) + (_outro?.Position ?? 0);
            }
            set
            {
                long introPosition = Math.Min(value, _intro?.Length ?? 0);
                value -= introPosition;

                long midPosition = Math.Min(value, _mid.Length);
                value -= midPosition;

                long outroPosition = Math.Min(value, _outro?.Length ?? 0);

                if (_intro != null)
                {
                    if (introPosition == _intro.Length) // for some files in SA, moving the position to the end causes an internal error in NVorbis
                    {
                        _introFinished = true;
                    }
                    else
                    {
                        _intro.Position = introPosition;
                    }
                }

                if (midPosition == _mid.Length)
                {
                    _midFinished = true;
                }
                else
                {
                    _mid.Position = midPosition;
                }

                if (_outro != null)
                {
                    _outro.Position = outroPosition;
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return OngoingStream.Read(buffer, offset, count);
        }
        
        protected override void Dispose(bool disposing)
        {
            //base.Dispose(disposing);

            //_intro?.Dispose();
            //_mid.Dispose();
            //_outro?.Dispose();
        }
    }
}