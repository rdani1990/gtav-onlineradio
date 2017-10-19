using System;
using System.Collections.Generic;

namespace RadioExpansion.Core.Logging
{
    public interface ILogger
    {
        void Log(string line, params object[] args);

        void LogTrack(string radio, MetaData track);

        void Close();
    }
}
