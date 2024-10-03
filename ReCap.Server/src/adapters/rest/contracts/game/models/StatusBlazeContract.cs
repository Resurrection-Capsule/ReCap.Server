namespace ReCap.Server.Adapters.Rest.Contracts.Game.Models.StatusBlaze;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

[XmlRoot("blaze")]
public class StatusBlazeContract {

    [XmlElement(ElementName = "health")]
    public int? Health { get; set; }
}
