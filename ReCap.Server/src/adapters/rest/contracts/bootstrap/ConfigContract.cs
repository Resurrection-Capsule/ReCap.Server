namespace ReCap.Server.Adapters.Rest.Contracts.Bootstrap.Config;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

[XmlRoot("config")]
public class ConfigContract {

    [XmlElement(ElementName = "blaze_service_name")]
    public string? BlazeServiceName { get; set; }

    [XmlElement(ElementName = "blaze_secure")]
    public string? BlazeSecure { get; set; }

    [XmlElement(ElementName = "blaze_env")]
    public string? BlazeEnv { get; set; }

    [XmlElement(ElementName = "sporenet_cdn_host")]
    public string? SporenetCdnHost { get; set; }

    [XmlElement(ElementName = "sporenet_db_host")]
    public string? SporenetDbHost { get; set; }

    [XmlElement(ElementName = "sporenet_db_name")]
    public string? SporenetDbName { get; set; }

    [XmlElement(ElementName = "sporenet_host")]
    public string? SporenetHost { get; set; }

    [XmlElement(ElementName = "http_secure")]
    public string? HttpSecure { get; set; }

    [XmlElement(ElementName = "liferay_host")]
    public string? LiferayHost { get; set; }

    [XmlElement(ElementName = "launcher_action")]
    public int? LauncherAction { get; set; }

    [XmlElement(ElementName = "launcher_url")]
    public string? LauncherUrl { get; set; }
}