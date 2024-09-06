using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer;

[XmlRoot("feed")]
public class FeedContract {

    [XmlArray("items")]
    [XmlArrayItem("item")]
    public List<FeedItemContract>? Items { get; set; }
}