using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HttpServer;

[XmlRoot("nucleus")]
public class StatusNucleusContract {

    [XmlElement(ElementName = "health")]
    public int? Health { get; set; }
}
