using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace RadioExpansion.Core
{
    public static class MetaHelper
    {
        /// <summary>
        /// Read the upcoming metadata info from the stream.
        /// </summary>
        public static OnlineStreamMetaData ReadMetaData(Stream sourceStream)
        {
            int metaLength = sourceStream.ReadByte() * 16; // the first byte marks the length of the info, multiplied by 16
            if (metaLength > 0) // if it's zero, then the metadata didn't change
            {
                var metaInfo = new byte[metaLength];
                int bytesRead = 0;
                while ((bytesRead += sourceStream.Read(metaInfo, bytesRead, metaLength - bytesRead)) < metaInfo.Length) ; // read the metainfo into the array

                string metaInfoFull = Encoding.UTF8.GetString(metaInfo); // e. g. "StreamTitle='Planet Full Of Blues - Felt Like A Tourist';StreamUrl='&artist=Planet%20Full%20Of%20Blues&title=Felt%20Like%20A%20Tourist&album=Hard%20Landing&duration=189544&songtype=S&overlay=NO&buycd=&website=&picture=az_B929423_Hard%20Landing_Planet%20Full%20Of%20B';\0\0\0\0"
                var infoRegex = Regex.Match(metaInfoFull, "StreamTitle='(.*?)';(StreamUrl='(.*?)')?");
                if (infoRegex.Success)
                {
                    string streamTitle = infoRegex.Groups[1].Value;
                    string streamUrl = infoRegex.Groups[2].Value;
                    var urlInfo = HttpUtility.ParseQueryString(streamUrl);
                    return new OnlineStreamMetaData()
                    {
                        Artist = urlInfo["artist"],
                        Track = urlInfo["title"],
                        StreamTitle = streamTitle,
                        StreamUrl = streamUrl
                    };
                }
            }

            return null;
        }
    }
}
