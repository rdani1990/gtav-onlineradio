using System.IO;

namespace RadioExpansion.Core.PlaylistReaders
{
    public static class PlaylistHelper
    {
        public static string[] ProcessPlaylist(string path)
        {
            switch (Path.GetExtension(path).ToLower())
            {
                case ".pls":
                    using (var reader = new PlsPlaylistReader(path))
                    {
                        return reader.ReadFiles();
                    }
                case ".m3u":
                case ".m3u8":
                    using (var reader = new M3uPlaylistReader(path))
                    {
                        return reader.ReadFiles();
                    }
                default:
                    return new string[0];
            }
        }
    }
}
