namespace ReCap.Server.Adapters.Rest.Contracts.Game.Models.Deck;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using ReCap.Server.Adapters.Rest.Contracts.Game.Models.Creature;

[XmlRoot("deck")]
public class DeckContract {

    [XmlElement(ElementName = "id")]
    public ulong? ID { get; set; }

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
    public List<CreatureContract>? Creatures { get; set; }

}
