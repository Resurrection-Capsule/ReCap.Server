using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer;

[XmlRoot("response")]
public class AuthResponseContract : ResponseContract {

    [XmlElement(ElementName = "account")]
    public AccountContract? Account { get; set; }

    [XmlArray("creatures")]
    [XmlArrayItem("creature")]
    public List<CreatureContract>? Creatures { get; set; }

    [XmlArray("decks")]
    [XmlArrayItem("deck")]
    public List<DeckContract>? Decks { get; set; }

    [XmlElement(ElementName = "feed")]
    public FeedContract? Feed { get; set; }

    [XmlElement(ElementName = "settings")]
    public SettingsContract? Settings { get; set; }

    [XmlElement(ElementName = "server_tuning")]
    public ServerTuningContract? ServerTuning { get; set; }
}