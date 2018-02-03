using Microsoft.Win32;
using RadioExpansion.Core.Logging;
using RadioExpansion.Core.RadioPlayers;
using RadioExpansion.Core.Serialization;
using System;
using System.IO;

namespace RadioExpansion.Core
{
    public static class RadioConfigManager
    {
        private const string RadioDirectory = "radios";
        private const string RadioFile = "radios.xml";

        private static string _radioFolder;

        public static string GetRadioFolder()
        {
            if (String.IsNullOrEmpty(_radioFolder))
            {
                // checking at the installation path of GTA V
                _radioFolder = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", "InstallFolder", null);

                if (String.IsNullOrEmpty(_radioFolder))
                {
                    Logger.Log("GTA V folder not found.");
                }
                else if (Directory.Exists(_radioFolder))
                {
                    _radioFolder = Path.Combine(_radioFolder, RadioDirectory);

                    Logger.Log("GTA V found. Using folder '{0}'.", _radioFolder);

                    return _radioFolder;
                }

                // fallback to the working directory
                // as an ASI plugin, it's the main directory of GTA V, since ScriptHookVDotNet.dll runs there. For other tools, it's the tool directory itself.
                _radioFolder = Path.Combine(Directory.GetCurrentDirectory(), RadioDirectory);
                Logger.Log("Using default folder '{0}'.", _radioFolder);
            }

            return _radioFolder;
        }

        public static Config LoadConfig()
        {
            string configPath = Path.Combine(GetRadioFolder(), RadioFile);
            bool exists = File.Exists(configPath);
            Logger.Log("Looking for config file '{0}'... File {1}.", configPath, exists ? "found" : "not found");

            if (exists)
            {
                try
                {
                    Logger.Log("Processing config file...");

                    var config = new ConfigSerializer().Deserialize(configPath);

                    foreach (var radio in config.Radios)
                    {
                        radio.StartMetaSyncing();
                    }

                    return config;
                }
                catch (Exception ex)
                {
                    Logger.Log("Failed to load config file. Is it a valid XML file? Error: {0}", ex);
                }
            }

            return null;
        }
        
        public static void Save(Radio[] radios)
        {
            var serializer = new ConfigSerializer();
            serializer.Serialize(Path.Combine(GetRadioFolder(), RadioFile), new Config()
            {
                Radios = radios
            });
        }
    }
}
