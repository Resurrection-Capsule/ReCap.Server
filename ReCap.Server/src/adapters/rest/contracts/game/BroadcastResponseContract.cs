using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

using ReCap.Server.Adapters.Rest.Contracts.Response;

namespace HttpServer;

[XmlRoot("response")]
public class BroadcastResponseContract : ResponseContract {

    [XmlArray("broadcasts")]
    [XmlArrayItem("broadcast")]
    public List<BroadcastContract>? Broadcasts { get; set; }
}