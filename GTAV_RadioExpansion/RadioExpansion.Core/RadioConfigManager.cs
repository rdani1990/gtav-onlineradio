using Microsoft.Win32;
using RadioExpansion.Core.Logging;
using RadioExpansion.Core.PlaylistReaders;
using RadioExpansion.Core.RadioPlayers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace RadioExpansion.Core
{
    public static class RadioConfigManager
    {
        private const string RADIO_DIRECTORY = "radios";

        public static string GetRadioFolder()
        {
            // checking at the installation path of GTA V
            string folder = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", "InstallFolder", null);

            if (String.IsNullOrEmpty(folder))
            {
                Logger.Log("GTA V folder not found.");
            }
            else if (Directory.Exists(folder))
            {
                folder = Path.Combine(folder, RADIO_DIRECTORY);

                Logger.Log("GTA V found. Using folder '{0}'.", folder);

                return folder;
            }

            // checking at the folder of the executing assembly
            // as an ASI plugin, it's the main directory of GTA V, since ScriptHookVDotNet.dll runs there. For other tools, it's the tool directory itself.
            folder = Path.Combine(Directory.GetCurrentDirectory(), RADIO_DIRECTORY);
            Logger.Log("Using default folder '{0}'.", folder);

            return folder;
        }

        private static Dictionary<string, XElement> ReadConfig(string radioFolder)
        {
            string configPath = Path.Combine(radioFolder, "radios.xml");
            bool exists = File.Exists(configPath);
            Logger.Log("Looking for config file '{0}'... File {1}.", configPath, exists ? "found" : "not found");

            var config = new Dictionary<string, XElement>();
            if (exists)
            {
                try
                {
                    Logger.Log("Processing config file...");
                    var doc = XDocument.Load(configPath);
                    foreach (var node in doc.Root.Elements())
                    {
                        string path = node.Attribute("path")?.Value;

                        if (path == null)
                        {
                            string nodeString = node.ToString(SaveOptions.DisableFormatting);
                            Logger.Log("Missing 'path' attribute on XML node: '{0}'", nodeString.Substring(0, Math.Min(nodeString.Length, 100)) + (nodeString.Length > 100 ? "..." : ""));
                            continue;
                        }

                        path = Path.Combine(radioFolder, path);

                        if (!File.Exists(path) && !Directory.Exists(path))
                        {
                            Logger.Log("Path '{0}' doesn't exist.", path);
                            continue;
                        }

                        if (config.ContainsKey(path))
                        {
                            Logger.Log("Duplicated path '{0}'.", path);
                            continue;
                        }

                        config.Add(Path.GetFileName(path), node);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("Failed to load config file. Is it a valid XML file? Error: {0}", ex);
                }
            }

            return config;
        }

        private static List<string> GetSortedRadioDirectories(string rootFolder, Dictionary<string, XElement> config)
        {
            var configKeys = config.Keys.ToList(); // config keys are "path" attribute of the Radio xml nodes
            var dirs = Directory.GetDirectories(rootFolder).ToList();

            dirs.Sort((a, b) =>
            {
                int indexA = configKeys.IndexOf(Path.GetFileName(a)); // position of the xml node A
                int indexB = configKeys.IndexOf(Path.GetFileName(b)); // position of the xml node B
                int comparisionResult = indexA.CompareTo(indexB);

                if ((indexA == -1 && indexB != -1) || // if "a" was NOT found in the config, but "b" was, then "a" goes to the end of the list
                    (indexA != -1 && indexB == -1)) // if "a" was found in the config, but "b" was NOT, then "b" goes to the end of the list
                {
                    comparisionResult *= -1; // for both above, the comparision result have to be flipped
                }

                return comparisionResult;
            });

            return dirs;
        }

        public static Radio[] LoadRadios()
        {
            string rootFolder = GetRadioFolder();

            if (rootFolder == null)
            {
                Logger.Log("No folder found for radios.");
                return null;
            }

            var config = ReadConfig(rootFolder);
            var radios = new List<Radio>();

            foreach (string radioFolder in GetSortedRadioDirectories(rootFolder, config))
            {
                string key = Path.GetFileName(radioFolder);
                if (key.ToLower() != "[adverts]")
                {
                    var allFiles = Directory.GetFiles(radioFolder);
                    var audioFiles = allFiles.Where(f => Radio.HasAllowedAudioExtension(f));
                    var playlistFiles = allFiles.Where(f => Radio.HasAllowedPlaylistExtension(f));

                    if (playlistFiles.Count() > 0)
                    {
                        if (playlistFiles.Count() > 1)
                        {
                            Logger.Log($"{playlistFiles.Count()} playlists were found. Only one playlist is allowed! Took the first one, ignored the rest.");
                        }

                        var filesInPlaylist = ProcessPlaylist(playlistFiles.First());
                        if (filesInPlaylist.Length == 1 && filesInPlaylist[0].StartsWith("http"))
                        {
                            var radio = new OnlineRadio(radioFolder, new Uri(filesInPlaylist[0]), config.ContainsKey(key) ? config[key] : null);
                            radios.Add(radio);
                            Logger.Log($"Added online radio '{radio.Name}'.");
                        }
                        else
                        {
                            var radio = new ClusteredRadio(radioFolder, filesInPlaylist, config.ContainsKey(key) ? config[key] : null);
                            radios.Add(radio);
                            Logger.Log($"Added clustered radio station '{radio.Name}' with {radio.TrackCount} music tracks and {radio.AdvertCount} adverts.");
                        }
                    }
                    else if (audioFiles.Count() == 1)
                    {
                        string mp3File = audioFiles.First();
                        if (Path.GetExtension(mp3File).ToLower() == ".mp3")
                        {
                            var radio = new LocalRadio(audioFiles.First(), config.ContainsKey(key) ? config[key] : null);
                            radios.Add(radio);
                            Logger.Log($"Added single-track radio file '{radio.Name}'.");
                        }
                        else
                        {
                            Logger.Log($"Skipped folder '{key}'. If a folder contains only a single audio file, then the audio file must be in MP3 format.");
                        }
                    }
                    else if (audioFiles.Count() > 1)
                    {
                        var radio = new ClusteredRadio(radioFolder, audioFiles, config.ContainsKey(key) ? config[key] : null);
                        radios.Add(radio);
                        Logger.Log($"Added clustered radio station '{radio.Name}' with {radio.TrackCount} music tracks and {radio.AdvertCount} adverts.");
                    }
                }
            }

            return radios.ToArray();
        }

        private static string[] ProcessPlaylist(string path)
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
