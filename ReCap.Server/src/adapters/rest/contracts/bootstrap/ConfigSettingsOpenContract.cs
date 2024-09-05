using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HttpServer;

[XmlRoot("open")]
public class ConfigSettingsOpenContract {

    [XmlAttribute("test")]
    public bool Test { get; set; }

    [XmlText]
    public bool Value { get; set; }
}
