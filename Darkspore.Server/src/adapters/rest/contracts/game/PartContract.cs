using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HttpServer;

[XmlRoot("part")]
public class PartContract
{
    [XmlElement(ElementName = "creature_id")]
    public int CreatureId { get; set; }


    [XmlElement(ElementName = "creation_date")]
    public ulong CreationDate { get; set; }

    [XmlElement(ElementName = "cost")]
    public int Cost { get; set; }
    
    [XmlElement(ElementName = "level")]
    public int Level { get; set; }
    

    [XmlElement(ElementName = "rarity")]
    public PartRarity Rarity { get; set; }
    
    [XmlElement(ElementName = "market_status")]
    public int MarketStatus { get; set; }
    
    [XmlElement(ElementName = "status")]
    public int Status { get; set; }
    
    [XmlElement(ElementName = "usage")]
    public int Usage { get; set; }

    
    [XmlElement(ElementName = "is_flair")]
    public bool IsFlair { get; set; }


    [XmlElement(ElementName = "rigblock_asset_id")]
    public int RigblockAssetHash { get; set; }
    
    [XmlElement(ElementName = "prefix_asset_id")]
    public int PrefixAssetHash { get; set; }
    
    [XmlElement(ElementName = "prefix_secondary_asset_id")]
    public int PrefixSecondaryAssetHash { get; set; }
    
    [XmlElement(ElementName = "suffix_asset_id")]
    public int SuffixAssetHash { get; set; }
}