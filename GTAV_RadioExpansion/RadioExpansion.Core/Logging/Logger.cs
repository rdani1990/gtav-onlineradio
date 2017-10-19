using System;

namespace RadioExpansion.Core.Logging
{
    public static class Logger
    {
        private static object _lock = new object();
        private static ILogger _logger;

        public static void SetLogger(ILogger logger)
        {
            _logger = logger;
        }
        
        public static void Log(string line, params object[] args)
        {
            if (_logger != null)
            {
                lock (_lock)
                {
                    _logger.Log(line, args);
                } 
            }
        }

        public static void LogTrack(string radio, MetaData track)
        {
            if (_logger != null && !String.IsNullOrEmpty(track?.ToString()))
            {
                lock (_lock)
                {
                    _logger.LogTrack(radio, track);
                }
            }
        }

        public static void Close()
        {
            _logger?.Close();
        }

    }
}
