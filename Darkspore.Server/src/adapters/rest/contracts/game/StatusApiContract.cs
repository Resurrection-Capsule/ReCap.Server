using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HttpServer;

[XmlRoot("api")]
public class StatusApiContract {

    [XmlElement(ElementName = "health")]
    public int? Health { get; set; }

    [XmlElement(ElementName = "revision")]
    public int? Revision { get; set; }

    [XmlElement(ElementName = "version")]
    public int? Version { get; set; }
}
