using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace RadioExpansion.Core.Serialization
{
    public class ConfigSerializer
    {
        private static XmlSerializer _serializer;

        static ConfigSerializer()
        {
            var attrOverrides = new XmlAttributeOverrides();
            var xmlIgnoreAttr = new XmlAttributes()
            {
                XmlIgnore = true
            };

            // get all the types in the current assembly
            foreach (var type in typeof(Config).Assembly.GetTypes())
            {
                // and if it has XmlWhitelistSerializationAttribute
                if (type.CustomAttributes.Any(a => a.AttributeType == typeof(XmlWhitelistSerializationAttribute)))
                {
                    // then serialize only those properties that have XmlWhitelistedAttribute. Others get the default XmlIgnore attribute.
                    foreach (var item in type.GetProperties())
                    {
                        if (!item.CustomAttributes.Any(a => a.AttributeType == typeof(XmlWhitelistedAttribute)))
                        {
                            attrOverrides.Add(type, item.Name, xmlIgnoreAttr);
                        }
                    }
                }
            }

            _serializer = new XmlSerializer(typeof(Config), attrOverrides);
        }

        public Config Deserialize(string configPath)
        {
            using (var reader = new StreamReader(configPath))
            {
                return (Config)_serializer.Deserialize(reader);
            }
        }

        public void Serialize(string configPath, Config config)
        {
            using (var reader = new StreamWriter(configPath))
            {
                _serializer.Serialize(reader, config);
            }
        }
    }
}
