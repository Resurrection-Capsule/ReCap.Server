using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace ReCap.Server.Adapters.Rest.Contracts.Game.Models;

[XmlRoot("server_tuning")]
public class ServerTuningContract {

    [XmlElement(ElementName = "itemstore_offer_period")]
    public int? ItemstoreOfferPeriod { get; set; }

    [XmlElement(ElementName = "itemstore_current_expiration")]
    public int? ItemstoreCurrentExpiration { get; set; }

    [XmlElement(ElementName = "itemstore_cost_multiplier_basic")]
    public int? ItemstoreCostMultiplierBasic { get; set; }

    [XmlElement(ElementName = "itemstore_cost_multiplier_uncommon")]
    public double? ItemstoreCostMultiplierUncommon { get; set; }

    [XmlElement(ElementName = "itemstore_cost_multiplier_rare")]
    public double? ItemstoreCostMultiplierRare { get; set; }

    [XmlElement(ElementName = "itemstore_cost_multiplier_epic")]
    public double? ItemstoreCostMultiplierEpic { get; set; }

    [XmlElement(ElementName = "itemstore_cost_multiplier_unique")]
    public double? ItemstoreCostMultiplierUnique { get; set; }

    [XmlElement(ElementName = "itemstore_cost_multiplier_rareunique")]
    public double? ItemstoreCostMultiplierRareUnique { get; set; }

    [XmlElement(ElementName = "itemstore_cost_multiplier_epicunique")]
    public double? ItemstoreCostMultiplierEpicUnique { get; set; }
}
