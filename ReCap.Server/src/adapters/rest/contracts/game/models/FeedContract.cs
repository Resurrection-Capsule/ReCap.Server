namespace ReCap.Server.Adapters.Rest.Contracts.Game.Models.Feed;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using ReCap.Server.Adapters.Rest.Contracts.Game.Models.FeedItem;

[XmlRoot("feed")]
public class FeedContract {

    [XmlArray("items")]
    [XmlArrayItem("item")]
    public List<FeedItemContract>? Items { get; set; }
}