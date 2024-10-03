namespace ReCap.Server.Adapters.Rest.Contracts.Game.Models.Broadcast;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

[XmlRoot("broadcast")]
public class BroadcastContract {

    [XmlElement(ElementName = "id")]
    public int? Id { get; set; }

    [XmlElement(ElementName = "end")]
    public int? End { get; set; }

    [XmlElement(ElementName = "start")]
    public int? Start { get; set; }

    [XmlElement(ElementName = "type")]
    public int? Type { get; set; }

    [XmlElement(ElementName = "message")]
    public string? Message { get; set; }

    [XmlElement(ElementName = "tokens")]
    public string? Tokens { get; set; }
}
