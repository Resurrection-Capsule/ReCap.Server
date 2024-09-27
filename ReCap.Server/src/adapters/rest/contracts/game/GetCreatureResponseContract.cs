using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer;

[XmlRoot("response")]
public class GetCreatureResponseContract : GetCreatureTemplateResponseContract {

    [XmlElement(ElementName = "id")]
    public ulong? ID { get; set; }

    [XmlElement(ElementName = "creator_id")]
    public ulong? AccountID { get; set; }

    [XmlElement(ElementName = "version")]
    public int? Version { get; set; }

    [XmlElement(ElementName = "noun_id")]
    public ulong? TemplateID { get; set; }

    [XmlElement(ElementName = "gear_score")]
    public double? GearScore { get; set; }

    [XmlElement(ElementName = "item_points")]
    public double? ItemPoints { get; set; }

    [XmlElement(ElementName = "png_large_url")]
    public string? LargePngUrl { get; set; }

    [XmlElement(ElementName = "png_thumb_url")]
    public string? ThumbPngUrl { get; set; }

    [XmlElement(ElementName = "stats")]
    public string? Stats { get; set; }
}