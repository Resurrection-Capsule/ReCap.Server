using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer;

[XmlRoot("response")]
public class GetAccountResponseContract : ResponseContract {

    [XmlElement(ElementName = "account")]
    public AccountContract? Account { get; set; }

    [XmlArray("decks")]
    [XmlArrayItem("deck")]
    public List<DeckContract>? Decks { get; set; }
}