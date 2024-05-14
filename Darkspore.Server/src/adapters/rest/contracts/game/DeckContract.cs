using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HttpServer;

[XmlRoot("deck")]
public class DeckContract {

    [XmlElement(ElementName = "id")]
    public int? ID { get; set; }

    [XmlElement(ElementName = "name")]
    public string? Name { get; set; }

    [XmlElement(ElementName = "category")]
    public string? Category { get; set; }

    [XmlElement(ElementName = "slot")]
    public int? Slot { get; set; }

    [XmlElement(ElementName = "locked")]
    public int? Locked { get; set; }

    [XmlArray("creatures")]
    [XmlArrayItem("creature")]
    public List<DeckCreatureContract>? Creatures { get; set; }

}
