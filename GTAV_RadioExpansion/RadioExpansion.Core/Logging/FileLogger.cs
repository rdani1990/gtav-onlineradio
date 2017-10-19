using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RadioExpansion.Core.Logging
{
    public class FileLogger : ILogger
    {
        private StreamWriter _logger;
        private StreamWriter _trackLogger;

        public FileLogger()
        {
            string radioFolder = RadioConfigManager.GetRadioFolder();
            Directory.CreateDirectory(radioFolder);

            _logger = new StreamWriter(Path.Combine(radioFolder, "GTAV_OnlineRadio.log"), true);
            _logger.AutoFlush = true;
            _trackLogger = new StreamWriter(Path.Combine(radioFolder, "GTAV_OnlineRadio_Tracks.log"), true);
            _trackLogger.AutoFlush = true;
        }

        public void Close()
        {
            _logger.Close();
            _trackLogger.Close();
        }

        public void Log(string line, params object[] args)
        {
            _logger.WriteLine("[{0}]\t{1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), String.Format(line, args));
        }

        public void LogTrack(string radio, MetaData track)
        {
            if (!String.IsNullOrEmpty(track?.ToString()))
            {
                _trackLogger.WriteLine("[{0}   {1}]\t{2}", DateTime.Now.ToString("yyyy-MM-dd HH:mm"), radio, track);
            }
        }
    }
}
