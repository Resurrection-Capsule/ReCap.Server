using ReCap.Server.Adapters.Rest.Contracts.Game.Models;
using ReCap.Server.Domain;
using ReCap.Server.Models;

namespace ReCap.Server.Mappers;

public class CreaturePartMapper
{
    public CreaturePartModel toModel(CreaturePart creaturePart) => new()
    {
        ID = creaturePart.ID,
        AccountId = creaturePart.AccountId,
        CreatureId = creaturePart.CreatureId,
        CreationDate = creaturePart.CreationDate,
        Cost = creaturePart.Cost,
        Level = creaturePart.Level,
        Rarity = creaturePart.Rarity,
        MarketStatus = creaturePart.MarketStatus,
        Status = creaturePart.Status,
        Usage = creaturePart.Usage,
        IsFlair = creaturePart.IsFlair,
        RigblockAssetHash = creaturePart.RigblockAssetHash,
        PrefixAssetHash = creaturePart.PrefixAssetHash,
        PrefixSecondaryAssetHash = creaturePart.PrefixSecondaryAssetHash,
        SuffixAssetHash = creaturePart.SuffixAssetHash,
        RigblockAssetId = creaturePart.RigblockAssetId,
        PrefixAssetId = creaturePart.PrefixAssetId,
        PrefixSecondaryAssetId = creaturePart.PrefixSecondaryAssetId,
        SuffixAssetId = creaturePart.SuffixAssetId,
    };

    public CreaturePartContract toContract(CreaturePartModel creaturePart) => new()
    {
        ID = creaturePart.ID,
        ReferenceID = creaturePart.ID,
        CreatureId = creaturePart.CreatureId ?? 0,
        CreationDate = creaturePart.CreationDate,
        Cost = creaturePart.Cost,
        Level = creaturePart.Level,
        Rarity = (int)creaturePart.Rarity,
        MarketStatus = creaturePart.MarketStatus,
        Status = creaturePart.Status,
        Usage = creaturePart.Usage,
        IsFlair = creaturePart.IsFlair ? 1 : 0,
        RigblockAssetHash = creaturePart.RigblockAssetHash,
        PrefixAssetHash = creaturePart.PrefixAssetHash,
        PrefixSecondaryAssetHash = creaturePart.PrefixSecondaryAssetHash,
        SuffixAssetHash = creaturePart.SuffixAssetHash,
    };

    private ulong fnv1aHashOfString(string val) {
        // C++ utils::hash_id (Functions.h:167) is 32-bit: uint32_t wraps at 2^32. The previous
        // ulong arithmetic never wrapped, so every hash diverged from the client's 32-bit value
        // (asset lookups failed). Compute in uint, zero-extend to the ulong wire field.
        uint h = 0x811C9DC5;
        foreach (char c in val)
        {
            h *= 0x01000193;
            h ^= c;
        }
        return h;
    }

    private ulong hashOfRigblock(ulong rigblock) {
		if (!(rigblock >= 1 && rigblock <= 1573) && !(rigblock >= 10001 && rigblock <= 10835)) {
			rigblock = 1;
		}
		return fnv1aHashOfString($"_Generated/LootRigblock{rigblock}.LootRigblock".ToLower());
	}

    private ulong hashOfPrefix(ulong prefix) {
		if (!(prefix >= 1 && prefix <= 338)) {
			prefix = 0;
		}
		return fnv1aHashOfString($"_Generated/LootPrefix{prefix}.LootPrefix".ToLower());
	}

	private ulong hashOfSuffix(ulong suffix) {
		if (!(suffix >= 1 && suffix <= 83) && !(suffix >= 10001 && suffix <= 10275)) {
			suffix = 0;
		}
		return fnv1aHashOfString($"_Generated/LootSuffix{suffix}.LootSuffix".ToLower());
	}

    public CreaturePartModel toCreaturePartModel(CreaturePartTemplateModel creaturePartTemplate, bool isDetail) {
        return new CreaturePartModel{
            CreationDate = 0, // TODO: current timestamp?

            Cost = creaturePartTemplate.cost,
            Level = creaturePartTemplate.level,

            Rarity = (CreaturePartRarity)creaturePartTemplate.rarity,
            MarketStatus = creaturePartTemplate.marketStatus,
            Status = creaturePartTemplate.status,
            Usage = creaturePartTemplate.usage,

            IsFlair = isDetail,

            RigblockAssetHash = hashOfRigblock(creaturePartTemplate.rigblockAssetId),
            PrefixAssetHash = hashOfPrefix(creaturePartTemplate.prefixAssetId),
            PrefixSecondaryAssetHash = hashOfPrefix(creaturePartTemplate.prefixSecondaryAssetId),
            SuffixAssetHash = hashOfSuffix(creaturePartTemplate.suffixAssetId),

            RigblockAssetId = creaturePartTemplate.rigblockAssetId,
            PrefixAssetId = creaturePartTemplate.prefixAssetId,
            PrefixSecondaryAssetId = creaturePartTemplate.prefixSecondaryAssetId,
            SuffixAssetId = creaturePartTemplate.suffixAssetId
        };
    }
}
