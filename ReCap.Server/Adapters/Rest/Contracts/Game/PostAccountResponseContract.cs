using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using ReCap.Server.Adapters.Rest.Contracts.Game.Models;

namespace ReCap.Server.Adapters.Rest.Contracts.Game;

[XmlRoot("response")]
public class PostAccountResponseContract : ResponseContract {

    [XmlElement(ElementName = "blaze_id")]
    public ulong? BlazeID { get; set; }

    [XmlElement(ElementName = "name")]
    public string? Name { get; set; }

    [XmlElement(ElementName = "grant_online_access")]
    public int? GrantOnlineAccess { get; set; }

    [XmlElement(ElementName = "cashout_bonus_time")]
    public int? CashoutBonusTime { get; set; }

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

    [XmlArray("stats")]
    [XmlArrayItem("stat")]
    public List<StatContract>? Stats { get; set; }
}