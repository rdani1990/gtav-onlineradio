using RadioExpansion.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using RadioExpansion.Core;

namespace RadioExpansion.TestConsolePlayer
{
    public class ConsoleLogger : ILogger
    {
        public void Close() { }

        public void Log(string line, params object[] args)
        {
            Console.WriteLine(line, args);
        }

        public void LogTrack(string radio, MetaData track)
        {
            Console.WriteLine("{0}: {1}", radio, track);
        }
    }
}
