namespace ReCap.Server.Adapters.Rest.Contracts.Game.Models.StatusGms;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

[XmlRoot("gms")]
public class StatusGmsContract {

    [XmlElement(ElementName = "health")]
    public int? Health { get; set; }
}
