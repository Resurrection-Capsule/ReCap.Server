using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer;

[XmlRoot("response")]
public class BroadcastResponseContract : ResponseContract {

    [XmlArray("broadcasts")]
    [XmlArrayItem("broadcast")]
    public List<BroadcastContract>? Broadcasts { get; set; }
}