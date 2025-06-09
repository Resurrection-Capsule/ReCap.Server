using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace ReCap.Server.Adapters.Rest.Contracts.Game.Models;

[XmlRoot("status")]
public class StatusContract {

    [XmlElement(ElementName = "api")]
    public StatusApiContract? Api { get; set; }

    [XmlElement(ElementName = "blaze")]
    public StatusBlazeContract? Blaze { get; set; }

    [XmlElement(ElementName = "gms")]
    public StatusGmsContract? Gms { get; set; }

    [XmlElement(ElementName = "nucleus")]
    public StatusNucleusContract? Nucleus { get; set; }

    [XmlElement(ElementName = "game")]
    public StatusGameContract? Game { get; set; }
}