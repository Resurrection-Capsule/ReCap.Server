using HttpServer;

namespace HttpServer;

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

    public int RigblockAssetHash { get; set; }
    public int PrefixAssetHash { get; set; }
    public int PrefixSecondaryAssetHash { get; set; }
    public int SuffixAssetHash { get; set; }

    public int RigblockAssetId { get; set; }
    public int PrefixAssetId { get; set; }
    public int PrefixSecondaryAssetId { get; set; }
    public int SuffixAssetId { get; set; }
}