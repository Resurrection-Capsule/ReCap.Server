using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using ReCap.Server.Adapters.Rest.Contracts.Game.Models;

namespace ReCap.Server.Adapters.Rest.Contracts.Game;

[XmlRoot("response")]
public class PartListResponseContract : ResponseContract {

    [XmlArray("parts")]
    [XmlArrayItem("part")]
    public List<CreaturePartContract>? Parts { get; set; }
}