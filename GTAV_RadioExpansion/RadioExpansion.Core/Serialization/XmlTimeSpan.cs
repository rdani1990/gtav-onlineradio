using System;
using System.Xml.Serialization;

namespace RadioExpansion.Core.Serialization
{
    /// <summary>
    /// Workaround class for TimeSpan serialization.
    /// </summary>
    public class XmlTimeSpan
    {
        private const long TICKS_PER_MS = TimeSpan.TicksPerMillisecond;

        private TimeSpan m_value = TimeSpan.Zero;

        public XmlTimeSpan() { }
        public XmlTimeSpan(TimeSpan source) { m_value = source; }

        public static implicit operator TimeSpan? (XmlTimeSpan o)
        {
            return o == null ? default(TimeSpan?) : o.m_value;
        }

        public static implicit operator XmlTimeSpan(TimeSpan? o)
        {
            return o == null ? null : new XmlTimeSpan(o.Value);
        }

        public static implicit operator TimeSpan(XmlTimeSpan o)
        {
            return o == null ? default(TimeSpan) : o.m_value;
        }

        public static implicit operator XmlTimeSpan(TimeSpan o)
        {
            return o == default(TimeSpan) ? null : new XmlTimeSpan(o);
        }

        [XmlText]
        public string Default
        {
            get { return m_value.ToString(@"h\:mm\:ss"); }
            set { m_value = TimeSpan.Parse(value); }
        }
    }
}
