using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

using ReCap.Server.Adapters.Rest.Contracts.Response;

namespace HttpServer;

[XmlRoot("response")]
public class PartListResponseContract : ResponseContract {

    [XmlArray("parts")]
    [XmlArrayItem("part")]
    public List<CreaturePartContract>? Parts { get; set; }
}