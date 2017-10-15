using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace GTAV_OnlineRadio.AsiLibrary
{
    public class MetaData
    {
        public virtual string Artist { get; set; }
        public virtual string Track { get; set; }

        public MetaData() { }

        public MetaData(string artist, string track)
        {
            Artist = artist?.Trim();
            Track = track?.Trim();
        }

        public override string ToString()
        {
            return (String.IsNullOrEmpty(Artist) ? Track : String.Format("{0} - {1}", Artist, Track));
        }
    }

    public class LocalStreamMetaData : MetaData
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }

        public LocalStreamMetaData(XElement xmlNode) : base(xmlNode.Element("Artist")?.Value, xmlNode.Element("Title")?.Value)
        {
            Start = TimeSpan.Parse(xmlNode.Element("Start").Value);
            End = TimeSpan.Parse(xmlNode.Element("End").Value);
        }
    }

    public class OnlineStreamMetaData : MetaData
    {
        private string _artist;
        private string _track;

        public override string Artist
        {
            get
            {
                // if artist is not set, but the StreamTitle is in "Artist - My Song" format, then we get the info from StreamTitle.
                if (String.IsNullOrWhiteSpace(_artist) && !String.IsNullOrEmpty(StreamTitle) && StreamTitle.Count(c => c == '-') == 1)
                {
                    return StreamTitle.Split('-')[0].Trim();
                }
                else
                {
                    return _artist;
                }
            }
            set
            {
                _artist = value?.Trim();
            }
        }

        public override string Track
        {
            get
            {
                // if track is not set, but the StreamTitle is in "Artist - My Song" format, then we get the info from StreamTitle.
                if (String.IsNullOrWhiteSpace(_track) && !String.IsNullOrEmpty(StreamTitle) && StreamTitle.Count(c => c == '-') == 1)
                {
                    return StreamTitle.Split('-')[1].Trim();
                }
                else
                {
                    return _track;
                }
            }
            set
            {
                _track = value?.Trim();
            }
        }

        public string StreamTitle { get; set; }
        public string StreamUrl { get; set; }

        public override string ToString()
        {
            if (!String.IsNullOrEmpty(Track))
            {
                return base.ToString();
            }
            else
            {
                return StreamTitle;
            }
        }
    }

    public class MetaDataReceivedEventArgs : EventArgs
    {
        public MetaData MetaData { get; set; }

        public MetaDataReceivedEventArgs(MetaData metaData)
        {
            MetaData = metaData;
        }
    }

}
