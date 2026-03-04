namespace ReCap.Server.Domain;

public class CreaturePart
{
    public ulong ID { get; set; }
    public ulong AccountId { get; set; }
    public int? CreatureId { get; set; }
    public ulong CreationDate { get; set; }

    public int Cost { get; set; }
    public int Level { get; set; }

    public CreaturePartRarity Rarity { get; set; }
    public int MarketStatus { get; set; }
    public int Status { get; set; }
    public int Usage { get; set; }

    public bool IsFlair { get; set; }

    public ulong RigblockAssetHash { get; set; }
    public ulong PrefixAssetHash { get; set; }
    public ulong PrefixSecondaryAssetHash { get; set; }
    public ulong SuffixAssetHash { get; set; }

    public ulong RigblockAssetId { get; set; }
    public ulong PrefixAssetId { get; set; }
    public ulong PrefixSecondaryAssetId { get; set; }
    public ulong SuffixAssetId { get; set; }
}