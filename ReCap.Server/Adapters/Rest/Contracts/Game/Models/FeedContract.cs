using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace ReCap.Server.Adapters.Rest.Contracts.Game.Models;

[XmlRoot("feed")]
public class FeedContract {

    [XmlArray("items")]
    [XmlArrayItem("item")]
    public List<FeedItemContract>? Items { get; set; }
}