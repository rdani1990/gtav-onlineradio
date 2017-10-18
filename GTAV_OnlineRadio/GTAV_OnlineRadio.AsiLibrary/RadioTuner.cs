using GTAV_OnlineRadio.AsiLibrary.RadioPlayers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GTAV_OnlineRadio.AsiLibrary
{
    /// <summary>
    /// Class to handle radio stations
    /// </summary>
    public class RadioTuner : IDisposable
    {
        private const string RADIO_DIRECTORY = "radios";

        private Radio[] _radios;
        private int? _activeStationIndex; // the currently selected station
        private int? _nextStationIndex; // the next station which gonna start, if activated

        public event EventHandler RadioLoadingCompleted;

        public bool IsRadioOn => _radios.Any(r => r.IsPlaying);

        public bool HasRadios => (_radios.Length > 0);

        public Radio CurrentStation
        {
            get
            {
                return (_activeStationIndex.HasValue ? _radios[_activeStationIndex.Value] : null);
            }
            set
            {
                int? newStationIndex = (value == null ? null : (int?)Array.IndexOf(_radios, value));
                if (newStationIndex != _activeStationIndex) // do anything only if station really changed
                {
                    //bool wasPreviousRadioStationPlaying = (CurrentStation?.Player.IsPlaying == true); // the previous radio station was on
                    CurrentStation?.Stop(); // completely stop the radio station (it can be in paused state)

                    _activeStationIndex = newStationIndex;

                    if (_activeStationIndex.HasValue)
                    {
                        CurrentStation.Play();
                    }
                }
            }
        }

        private static RadioTuner _instance;

        public static RadioTuner Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new RadioTuner();
                }
                return _instance;
            }
        }

        private RadioTuner()
        {
            EnableUnsafeHeaderParsing();
            _radios = new Radio[0];
        }

        public void Play(int? stationIndex = null)
        {
            if (_radios.Length > 0)
            {
                CurrentStation = _radios[stationIndex ?? _activeStationIndex ?? 0];
                CurrentStation.Play();
            }
        }

        public void StopCurrent()
        {
            CurrentStation?.Stop();
        }

        public void PauseCurrent()
        {
            CurrentStation?.Pause();
        }

        public void KeepStreamAlive()
        {
            CurrentStation?.KeepAlive();
        }
        
        public void Dispose()
        {
            foreach (var radio in _radios)
            {
                radio.Dispose();
            }

            _instance = null;

            RadioLogoManager.Cleanup();
        }

        public Radio MoveToNextStation()
        {
            if (!_nextStationIndex.HasValue)
            {
                _nextStationIndex = _activeStationIndex;
            }
            
            if (_nextStationIndex == _radios.Length - 1)
            {
                _nextStationIndex = 0;
            }
            else
            {
                _nextStationIndex++;
            }

            return _radios[_nextStationIndex.Value];
        }

        public Radio MoveToPreviousStation()
        {
            if (!_nextStationIndex.HasValue)
            {
                _nextStationIndex = _activeStationIndex;
            }

            if (_nextStationIndex == 0)
            {
                _nextStationIndex = _radios.Length - 1;
            }
            else
            {
                _nextStationIndex--;
            }

            return _radios[_nextStationIndex.Value];
        }

        public void ActivateNextStation()
        {
            if (_nextStationIndex.HasValue)
            {
                CurrentStation = _radios[_nextStationIndex.Value];
                _nextStationIndex = null;
            }
        }

        public static string GetRadioFolder(bool addLog)
        {
            // checking at the installation path of GTA V
            string folder = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", "InstallFolder", null);
            bool exists = false;

            if (!String.IsNullOrEmpty(folder))
            {
                folder = Path.Combine(folder, RADIO_DIRECTORY);
                exists = Directory.Exists(folder);

                if (addLog)
                {
                    Logger.Instance.Log("Looking for folder '{0}'... Folder {1}.", folder, exists ? "found" : "not found");
                }
            }

            if (exists)
            {
                return folder;
            }

            // checking at the folder of the executing assembly
            // as an ASI plugin, it's the main directory of GTA V, since ScriptHookVDotNet.dll runs there. For other tools, it's the tool directory itself.
            folder = Path.Combine(Directory.GetCurrentDirectory(), RADIO_DIRECTORY);
            if (addLog)
            {
                Logger.Instance.Log("Using default folder '{0}'.", folder);
            }

            return folder;
        }

        private static Dictionary<string, XElement> ReadConfig(string radioFolder)
        {
            string configPath = Path.Combine(radioFolder, "radios.xml");
            bool exists = File.Exists(configPath);
            Logger.Instance.Log("Looking for config file '{0}'... File {1}.", configPath, exists ? "found" : "not found");

            var config = new Dictionary<string, XElement>();
            if (exists)
            {
                try
                {
                    Logger.Instance.Log("Processing config file...");
                    var doc = XDocument.Load(configPath);
                    foreach (var node in doc.Root.Elements())
                    {
                        string path = node.Attribute("path")?.Value;

                        if (path == null)
                        {
                            string nodeString = node.ToString(SaveOptions.DisableFormatting);
                            Logger.Instance.Log("Missing 'path' attribute on XML node: '{0}'", nodeString.Substring(0, Math.Min(nodeString.Length, 100)) + (nodeString.Length > 100 ? "..." : ""));
                            continue;
                        }

                        path = Path.Combine(radioFolder, path);

                        if (!File.Exists(path) && !Directory.Exists(path))
                        {
                            Logger.Instance.Log("Path '{0}' doesn't exist.", path);
                            continue;
                        }

                        if (config.ContainsKey(path))
                        {
                            Logger.Instance.Log("Duplicated path '{0}'.", path);
                            continue;
                        }

                        config.Add(Path.GetFileName(path), node);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log("Failed to load config file. Is it a valid XML file? Error: {0}", ex);
                }
            }

            return config;
        }

        private List<string> GetSortedRadioDirectories(string rootFolder, Dictionary<string, XElement> config)
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

        public void LoadRadios()
        {
            string rootFolder = GetRadioFolder(true);

            if (rootFolder == null)
            {
                Logger.Instance.Log("No folder found for radios.");
                return;
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
                            Logger.Instance.Log($"{playlistFiles.Count()} playlists were found. Only one playlist is allowed! Took the first one, ignored the rest.");
                        }

                        var filesInPlaylist = ProcessPlaylist(playlistFiles.First());
                        if (filesInPlaylist.Length == 1 && filesInPlaylist[0].StartsWith("http"))
                        {
                            var radio = new OnlineRadio(radioFolder, new Uri(filesInPlaylist[0]), config.ContainsKey(key) ? config[key] : null);
                            radios.Add(radio);
                            Logger.Instance.Log($"Added online radio '{radio.Name}'.");
                        }
                        else
                        {
                            var radio = new ClusteredRadio(radioFolder, filesInPlaylist, config.ContainsKey(key) ? config[key] : null);
                            radios.Add(radio);
                            Logger.Instance.Log($"Added clustered radio station '{radio.Name}' with {radio.TrackCount} music tracks and {radio.AdvertCount} adverts.");
                        }
                    }
                    else if (audioFiles.Count() == 1)
                    {
                        string mp3File = audioFiles.First();
                        if (Path.GetExtension(mp3File).ToLower() == ".mp3")
                        {
                            var radio = new LocalRadioPlayer(audioFiles.First(), config.ContainsKey(key) ? config[key] : null);
                            radios.Add(radio);
                            Logger.Instance.Log($"Added single-track radio file '{radio.Name}'.");
                        }
                        else
                        {
                            Logger.Instance.Log($"Skipped folder '{key}'. If a folder contains only a single audio file, then the audio file must be in MP3 format.");
                        }
                    }
                    else if (audioFiles.Count() > 1)
                    {
                        var radio = new ClusteredRadio(radioFolder, audioFiles, config.ContainsKey(key) ? config[key] : null);
                        radios.Add(radio);
                        Logger.Instance.Log($"Added clustered radio station '{radio.Name}' with {radio.TrackCount} music tracks and {radio.AdvertCount} adverts.");
                    }
                }
            }
            
            _radios = radios.ToArray();

            RadioLoadingCompleted?.Invoke(this, EventArgs.Empty);
        }
        
        private static string[] ProcessPlaylist(string path)
        {
            var files = new List<string>();

            switch (Path.GetExtension(path).ToLower())
            {
                case ".pls":
                    using (var sr = new StreamReader(path, Encoding.UTF8))
                    {
                        while (!sr.EndOfStream)
                        {
                            string line = sr.ReadLine();
                            var match = Regex.Match(line, "^File\\d=(.*)");
                            if (match.Success)
                            {
                                files.Add(match.Groups[1].Value);
                            }
                        }
                    }
                    break;
                case ".m3u":
                case ".m3u8":
                    using (var sr = new StreamReader(path, Encoding.UTF8))
                    {
                        while (!sr.EndOfStream)
                        {
                            string line = sr.ReadLine();
                            if (!line.StartsWith("#") && !String.IsNullOrWhiteSpace(line))
                            {
                                files.Add(line);
                            }
                        }
                    }
                    break;
            }

            return files.ToArray();
        }

        //private void LoadRadios()
        //{
        //    string path = Path.Combine("scripts", "radios.xml");
        //    if (!File.Exists(path))
        //    {
        //        path = "radios.xml";
        //    }

        //    var doc = XDocument.Load(path);
        //    _radios = doc.Root.Elements().Select(node => new Radio(node)).ToArray();

        //    RadioLogoManager.CreateTempLogos(_radios);
        //}

        //private void SaveRadios()
        //{
        //    var doc = new XDocument();
        //    var radios = new XElement("Radios");
        //    doc.Add(radios);
        //    foreach (var item in _radios)
        //    {
        //        var radio = new XElement("Radio");
        //        radio.Add(new XElement("Name", item.Name));
        //        radio.Add(new XElement("Uri", item.Uri));
        //        radio.Add(new XElement("Volume", item.Volume));
        //        radios.Add(radio);
        //    }
        //    doc.Save("radios.xml");
        //}

        public void LogCurrentTrack()
        {
            if (CurrentStation != null)
            {
                Logger.Instance.LogTrack(CurrentStation.Name, CurrentStation.CurrentTrackMetaData);
            }
        }

        /// <summary>
        /// Enable 'useUnsafeHeaderParsing', because .NET throws protocol violation exception for many shoutcast streams.
        /// See http://o2platform.wordpress.com/2010/10/20/dealing-with-the-server-committed-a-protocol-violation-sectionresponsestatusline/
        /// </summary>
        private static bool EnableUnsafeHeaderParsing()
        {
            //Get the assembly that contains the internal class
            Assembly assembly = Assembly.GetAssembly(typeof(SettingsSection));
            if (assembly != null)
            {
                //Use the assembly in order to get the internal type for the internal class
                Type settingsSectionType = assembly.GetType("System.Net.Configuration.SettingsSectionInternal");
                if (settingsSectionType != null)
                {
                    //Use the internal static property to get an instance of the internal settings class.
                    //If the static instance isn't created already invoking the property will create it for us.
                    object anInstance = settingsSectionType.InvokeMember("Section",
                    BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.NonPublic, null, null, new object[] { });
                    if (anInstance != null)
                    {
                        //Locate the private bool field that tells the framework if unsafe header parsing is allowed
                        FieldInfo aUseUnsafeHeaderParsing = settingsSectionType.GetField("useUnsafeHeaderParsing", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (aUseUnsafeHeaderParsing != null)
                        {
                            aUseUnsafeHeaderParsing.SetValue(anInstance, true);
                            return true;
                        }

                    }
                }
            }
            return false;
        }
    }
}
