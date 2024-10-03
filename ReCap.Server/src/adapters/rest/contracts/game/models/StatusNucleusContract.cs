namespace ReCap.Server.Adapters.Rest.Contracts.Game.Models.StatusNucleus;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

[XmlRoot("nucleus")]
public class StatusNucleusContract {

    [XmlElement(ElementName = "health")]
    public int? Health { get; set; }
}
