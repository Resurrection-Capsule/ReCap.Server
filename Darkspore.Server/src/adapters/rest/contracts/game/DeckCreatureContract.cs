using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HttpServer;

[XmlRoot("creature")]
public class DeckCreatureContract {

    [XmlElement(ElementName = "id")]
    public int? ID { get; set; }

    [XmlElement(ElementName = "name")]
    public string? Name { get; set; }

    [XmlElement(ElementName = "noun_id")]
    public int? NounID { get; set; }

    [XmlElement(ElementName = "version")]
    public int? Version { get; set; }

    [XmlElement(ElementName = "gear_score")]
    public float? GearScore { get; set; }

    [XmlElement(ElementName = "item_points")]
    public float? ItemPoints { get; set; }
}
