using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RadioExpansion.Core.PlaylistReaders
{
    public class M3uPlaylistReader : IDisposable
    {
        private StreamReader _fileReader;

        public M3uPlaylistReader(string playlistPath)
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
                if (!line.StartsWith("#") && !String.IsNullOrWhiteSpace(line))
                {
                    files.Add(line);
                }
            }

            return files.ToArray();
        }
    }
}
