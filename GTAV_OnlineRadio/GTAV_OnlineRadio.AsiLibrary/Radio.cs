using GTAV_OnlineRadio.AsiLibrary.RadioPlayers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace GTAV_OnlineRadio.AsiLibrary
{
    //public class Radió
    //{
    //    public string Name { get; set; }
    //    public string LogoPath { get; set; }
    //    public Radio Player { get; set; }
        
    //    public Radió(XElement xmlNode)
    //    {
    //        var uri = new Uri(xmlNode.Element("Uri").Value);
    //        string name = xmlNode.Element("Name").Value;
    //        float volume;
    //        if (!Single.TryParse(xmlNode.Element("Volume")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out volume))
    //        {
    //            volume = 1;
    //        }

    //        if (uri.IsFile)
    //        {
    //            var trackList = xmlNode.Element("TrackList");
    //            if (Directory.Exists(uri.LocalPath)) // in case URI points to a folder, we handle it as a ClusteredRadioPlayer
    //            {
    //                Player = new ClusteredRadio(name, uri, volume, trackList, xmlNode.Element("Adverts"));
    //            }
    //            else
    //            {
    //                Player = new LocalRadioPlayer(name, uri, volume, trackList);
    //            }
    //        }
    //        else
    //        {
    //            Player = new OnlineRadio(name, uri, volume);
    //        }

    //        Name = name;
    //        LogoPath = xmlNode.Element("LogoPath")?.Value;
    //    }
    //}
}