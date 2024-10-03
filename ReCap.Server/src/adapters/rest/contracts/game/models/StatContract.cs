namespace ReCap.Server.Adapters.Rest.Contracts.Game.Models.Stat;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

[XmlRoot("stat")]
public class StatContract {

    [XmlElement(ElementName = "wins")]
    public int? Wins { get; set; }
}
