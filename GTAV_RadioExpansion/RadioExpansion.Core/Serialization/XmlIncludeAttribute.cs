using System;

namespace RadioExpansion.Core.Serialization
{
    /// <summary>
    /// Classes with this attribute will serialize only those properties that has <see cref="XmlWhitelistedAttribute"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class XmlWhitelistSerializationAttribute : Attribute
    {
    }

    /// <summary>
    /// Property is included in XML serialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class XmlWhitelistedAttribute : Attribute
    {
    }
}
