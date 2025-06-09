using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace ReCap.Server.Adapters.Rest.Contracts.Bootstrap;

[XmlRoot("patches")]
public class ConfigPatchesContract {

    [XmlAttribute("target")]
    public string? Target { get; set; }

    [XmlAttribute("date")]
    public string? Date { get; set; }

    [XmlAttribute("from_version")]
    public string? FromVersion { get; set; }

    [XmlAttribute("to_version")]
    public string? ToVersion { get; set; }

    [XmlAttribute("id")]
    public string? ID { get; set; }

    [XmlAttribute("description")]
    public string? Description { get; set; }

    [XmlAttribute("application_instructions")]
    public string? ApplicationInstructions { get; set; }

    [XmlAttribute("locale")]
    public string? Locale { get; set; }

    [XmlAttribute("shipping")]
    public string? Shipping { get; set; }

    [XmlAttribute("file_url")]
    public string? FileUrl { get; set; }

    [XmlAttribute("archive_size")]
    public string? ArchiveSize { get; set; }

    [XmlAttribute("uncompressed_size")]
    public string? UncompressedSize { get; set; }

    [XmlAttribute("hashes")]
    public string? Hashes { get; set; }

    [XmlElement(ElementName = "hashes")]
    public string[]? HashesList { get; set; }
}
