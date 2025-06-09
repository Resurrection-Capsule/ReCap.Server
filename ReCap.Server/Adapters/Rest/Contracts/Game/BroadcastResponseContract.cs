using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using ReCap.Server.Adapters.Rest.Contracts.Game.Models;

namespace ReCap.Server.Adapters.Rest.Contracts.Game;

[XmlRoot("response")]
public class BroadcastResponseContract : ResponseContract {

    [XmlArray("broadcasts")]
    [XmlArrayItem("broadcast")]
    public List<BroadcastContract>? Broadcasts { get; set; }
}