using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HttpServer;

[XmlRoot("settings")]
public class SettingsContract {

    [XmlElement(ElementName = "showConfigAlerts")]
    public string? ShowConfigAlerts { get; set; }

    [XmlElement(ElementName = "cheat")]
    public string? Cheat { get; set; }

    [XmlElement(ElementName = "safeMode")]
    public string? SafeMode { get; set; }
}
