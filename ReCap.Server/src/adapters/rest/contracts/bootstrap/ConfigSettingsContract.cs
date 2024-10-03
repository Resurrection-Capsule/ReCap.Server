namespace ReCap.Server.Adapters.Rest.Contracts.Bootstrap.ConfigSettings;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using ReCap.Server.Adapters.Rest.Contracts.Bootstrap.ConfigSettingsOpen;

[XmlRoot("settings")]
public class ConfigSettingsContract {

    [XmlElement(ElementName = "open")]
    public ConfigSettingsOpenContract? Open { get; set; }

    [XmlElement(ElementName = "telemetry-rate")]
    public int? TelemetryRate { get; set; }

    [XmlElement(ElementName = "telemetry-setting")]
    public int? TelemetrySetting { get; set; }
}
