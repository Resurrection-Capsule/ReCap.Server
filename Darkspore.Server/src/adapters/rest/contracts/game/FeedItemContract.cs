using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer;

[XmlRoot("item")]
public class FeedItemContract {

    [XmlElement(ElementName = "id")]
    public int? FeedId { get; set; }

    [XmlElement(ElementName = "account_id")]
    public int? AccountId { get; set; }

    [XmlElement(ElementName = "message_id")]
    public int? MessageId { get; set; }

    [XmlElement(ElementName = "metadata")]
    public string? Metadata { get; set; }

    [XmlElement(ElementName = "name")]
    public string? Name { get; set; }

    [XmlElement(ElementName = "time")]
    public long? Time { get; set; }
}