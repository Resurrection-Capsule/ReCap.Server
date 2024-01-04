using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer
{
    [XmlRoot("response")]
    public class ConfigResponseContract {

        [XmlElement(ElementName = "configs")]
        public List<ConfigContract>? Configs { get; set; }

        [XmlElement(ElementName = "to_image")]
        public string? ToImage { get; set; }

        [XmlElement(ElementName = "from_image")]
        public string? FromImage { get; set; }
    }
}