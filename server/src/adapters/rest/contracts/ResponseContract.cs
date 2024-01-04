using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer;

[XmlRoot("response")]
public class ResponseContract {

    [XmlElement(ElementName = "stat")]
    public string? Stat { get; set; }

    [XmlElement(ElementName = "version")]
    public string? Version { get; set; }

    [XmlElement(ElementName = "timestamp")]
    public int? Timestamp { get; set; }

    [XmlElement(ElementName = "exectime")]
    public int? ExecTime { get; set; }
}