namespace ReCap.Server.Adapters.Rest.Contracts.Game.Models.Settings;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

[XmlRoot("settings")]
public class SettingsContract {

    [XmlElement(ElementName = "showConfigAlerts")]
    public string? ShowConfigAlerts { get; set; }

    [XmlElement(ElementName = "cheat")]
    public string? Cheat { get; set; }

    [XmlElement(ElementName = "safeMode")]
    public string? SafeMode { get; set; }
}
