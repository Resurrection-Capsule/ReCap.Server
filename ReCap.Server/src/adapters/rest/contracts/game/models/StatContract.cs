using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HttpServer;

[XmlRoot("stat")]
public class StatContract {

    [XmlElement(ElementName = "wins")]
    public int? Wins { get; set; }
}
