using RadioExpansion.Core.RadioPlayers;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace RadioExpansion.Core
{
    public class Config
    {
        private string _version;
        public string Version
        {
            get
            {
                if (_version == null)
                {
                    string version = FileVersionInfo.GetVersionInfo(GetType().Assembly.Location).FileVersion; // getting the version string of this assembly, e. g. "0.9.0.0"
                    var versionNumbers = Regex.Match(version, @"([0-9])+\.([0-9])+\.([0-9])+\.([0-9])+").Groups; // match the numbers with a regex ["0.9.0.0", "0", "9", "0", "0"]
                    var versionBuilder = new StringBuilder($"{versionNumbers[1]}.{versionNumbers[2]}"); // take version numbers from first two level. Group at zero index is the whole version, "0.9.0.0" 

                    // take the last two minor level only if they are not zeros
                    if (versionNumbers[3].Value != "0" || versionNumbers[4].Value != "0")
                    {
                        versionBuilder.Append($".{versionNumbers[3]}");
                    }
                    if (versionNumbers[4].Value != "0")
                    {
                        versionBuilder.Append($".{versionNumbers[4]}");
                    }

                    _version = versionBuilder.ToString(); // result is "0.9"
                }

                return _version;
            }
            set
            {
                _version = value;
            }
        }

        [XmlArrayItem(nameof(OnlineRadio), typeof(OnlineRadio))]
        [XmlArrayItem(nameof(LocalRadio), typeof(LocalRadio))]
        [XmlArrayItem(nameof(ClusteredRadio), typeof(ClusteredRadio))]
        public Radio[] Radios { get; set; }
    }
}
