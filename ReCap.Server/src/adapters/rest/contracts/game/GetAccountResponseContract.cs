namespace HttpServer;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

using ReCap.Server.Adapters.Rest.Contracts.Game.Models.Account;
using ReCap.Server.Adapters.Rest.Contracts.Game.Models.Deck;
using ReCap.Server.Adapters.Rest.Contracts.Response;

[XmlRoot("response")]
public class GetAccountResponseContract : ResponseContract {

    [XmlElement(ElementName = "account")]
    public AccountContract? Account { get; set; }

    [XmlArray("decks")]
    [XmlArrayItem("deck")]
    public List<DeckContract>? Decks { get; set; }
}