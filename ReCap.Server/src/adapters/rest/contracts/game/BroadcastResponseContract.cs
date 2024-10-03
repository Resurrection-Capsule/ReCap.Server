namespace ReCap.Server.Adapters.Rest.Contracts.Game.BroadcastResponse;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using ReCap.Server.Adapters.Rest.Contracts.Game.Models.Broadcast;
using ReCap.Server.Adapters.Rest.Contracts.Response;

[XmlRoot("response")]
public class BroadcastResponseContract : ResponseContract {

    [XmlArray("broadcasts")]
    [XmlArrayItem("broadcast")]
    public List<BroadcastContract>? Broadcasts { get; set; }
}