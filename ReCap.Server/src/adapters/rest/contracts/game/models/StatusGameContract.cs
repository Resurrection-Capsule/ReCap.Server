namespace ReCap.Server.Adapters.Rest.Contracts.Game.Models.StatusGame;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

[XmlRoot("game")]
public class StatusGameContract {

    [XmlElement(ElementName = "health")]
    public int? Health { get; set; }

    [XmlElement(ElementName = "countdown")]
    public int? Countdown { get; set; }

    [XmlElement(ElementName = "open")]
    public int? Open { get; set; }

    [XmlElement(ElementName = "throttle")]
    public int? Throttle { get; set; }

    [XmlElement(ElementName = "vip")]
    public int? Vip { get; set; }
}
