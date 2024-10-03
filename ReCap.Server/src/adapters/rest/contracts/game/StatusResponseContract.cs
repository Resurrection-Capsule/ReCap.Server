using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

using ReCap.Server.Adapters.Rest.Contracts.Response;

namespace HttpServer;

[XmlRoot("response")]
public class StatusResponseContract : ResponseContract {

    [XmlElement(ElementName = "status")]
    public StatusContract? Status { get; set; }

    [XmlArray("broadcasts")]
    [XmlArrayItem("broadcast")]
    public List<BroadcastContract>? Broadcasts { get; set; }
}