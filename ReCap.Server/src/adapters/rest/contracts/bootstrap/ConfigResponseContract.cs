namespace ReCap.Server.Adapters.Rest.Contracts.Bootstrap.ConfigResponse;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using ReCap.Server.Adapters.Rest.Contracts.Bootstrap.Config;
using ReCap.Server.Adapters.Rest.Contracts.Bootstrap.ConfigPatches;
using ReCap.Server.Adapters.Rest.Contracts.Bootstrap.ConfigSettings;
using ReCap.Server.Adapters.Rest.Contracts.Response;

[XmlRoot("response")]
public class ConfigResponseContract : ResponseContract {

    [XmlArray("configs")]
    [XmlArrayItem("config")]
    public List<ConfigContract>? Configs { get; set; }

    [XmlElement(ElementName = "to_image")]
    public string? ToImage { get; set; }

    [XmlElement(ElementName = "from_image")]
    public string? FromImage { get; set; }

    [XmlElement(ElementName = "settings")]
    public ConfigSettingsContract? Settings { get; set; }

    [XmlElement(ElementName = "patches")]
    public ConfigPatchesContract? Patches { get; set; }
}