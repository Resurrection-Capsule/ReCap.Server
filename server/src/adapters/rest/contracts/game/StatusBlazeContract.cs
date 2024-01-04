using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HttpServer;

[XmlRoot("blaze")]
public class StatusBlazeContract {

    [XmlElement(ElementName = "health")]
    public int? Health { get; set; }
}
