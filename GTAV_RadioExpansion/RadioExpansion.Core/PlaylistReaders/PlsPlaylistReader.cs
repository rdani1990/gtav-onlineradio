using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RadioExpansion.Core.PlaylistReaders
{
    public class PlsPlaylistReader : IDisposable
    {
        private StreamReader _fileReader;

        public PlsPlaylistReader(string playlistPath)
        {
            _fileReader = new StreamReader(playlistPath, Encoding.UTF8);
        }

        public void Dispose()
        {
            _fileReader.Dispose();
        }

        public string[] ReadFiles()
        {
            var files = new List<string>();

            while (!_fileReader.EndOfStream)
            {
                string line = _fileReader.ReadLine();
                var match = Regex.Match(line, "^File\\d+=(.*)");
                if (match.Success)
                {
                    files.Add(match.Groups[1].Value);
                }
            }

            return files.ToArray();
        }
    }
}
