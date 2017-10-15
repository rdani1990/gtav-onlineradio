using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GTAV_OnlineRadio.AsiLibrary
{
    public class Logger : IDisposable
    {
        private StreamWriter _logger;
        private StreamWriter _trackLogger;
        private object _lock = new object();

        private static Logger _instance;

        public static Logger Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Logger();
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        private Logger() { }

        private void Initialize()
        {
            _logger = new StreamWriter(Path.Combine(RadioTuner.GetRadioFolder(false), "GTAV_OnlineRadio.log"), true);
            _logger.AutoFlush = true;
            _trackLogger = new StreamWriter(Path.Combine(RadioTuner.GetRadioFolder(false), "GTAV_OnlineRadio_Tracks.log"), true);
            _trackLogger.AutoFlush = true;
        }

        public void Dispose()
        {
            _logger.Close();
            _trackLogger.Close();
        }
        
        public void Log(string line, params object[] args)
        {
            lock (_lock)
            {
                _logger.WriteLine("[{0}]\t{1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), String.Format(line, args));
            }
        }

        public void LogTrack(string radio, MetaData track)
        {
            if (!String.IsNullOrEmpty(track?.ToString()))
            {
                lock (_lock)
                {
                    _trackLogger.WriteLine("[{0}   {1}]\t{2}", DateTime.Now.ToString("yyyy-MM-dd HH:mm"), radio, track);
                }
            }
        }

    }
}
