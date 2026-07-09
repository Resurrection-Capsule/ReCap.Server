using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using ReCap.Server.Adapters.Rest.Contracts.Game.Models;

namespace ReCap.Server.Adapters.Rest.Contracts.Game;

[XmlRoot("response")]
public class PartListResponseContract : ResponseContract {

    [XmlArray("parts")]
    [XmlArrayItem("part")]
    public List<CreaturePartContract>? Parts { get; set; }

    // getPartOfferList: unix seconds the current offer set expires (ClientRest::ParsePartOfferListResponse obj+0x58).
    [XmlElement(ElementName = "expires")]
    public long? Expires { get; set; }
    public bool ShouldSerializeExpires() => Expires.HasValue;

    // vendorParts: player's DNA balance after the transaction batch (ClientRest::ParseVendorPartsResponse).
    [XmlElement(ElementName = "dna")]
    public int? Dna { get; set; }
    public bool ShouldSerializeDna() => Dna.HasValue;
}