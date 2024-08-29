using HttpServer;

namespace HttpServer;

public class CreaturePartService
{
    private CreaturePartRepositoryAdapter creaturePartRepository;
    private CreaturePartTemplateRepositoryAdapter creaturePartTemplateRepository;

    public CreaturePartService(SqliteConfig newSqliteConfig) {
        creaturePartRepository = new CreaturePartRepositoryAdapter(newSqliteConfig);
        creaturePartTemplateRepository = new CreaturePartTemplateRepositoryAdapter(newSqliteConfig);
    }

    public List<CreaturePartModel> getCreaturePartsByAccount(AccountModel account) {
        return creaturePartRepository.getCreaturePartsByAccountId(account.Id);
    }

    public List<CreaturePart> addAllCreatureParts(Account account) {
        List<CreaturePart> parts = [];
        var allTemplates = creaturePartTemplateRepository.getAllTemplates();
        foreach (var template in allTemplates) {
            parts.Add(addCreaturePart(account, template));
        }
        return parts;
    }

    private ulong fnv1aHashOfString(string val) {
        ulong h = 0x811c9dc5u;

        int size = val.Length;
        for (int i = 0; i < size; i++)
        {
            h ^= val[i];
            h *= 0x01000193u;
        }
    
        return h;
    }

    private ulong hashOfRigblock(ulong rigblock) {
		if (!(rigblock >= 1 && rigblock <= 1573) && !(rigblock >= 10001 && rigblock <= 10835)) {
			rigblock = 1;
		}
		return fnv1aHashOfString($"_Generated/LootRigblock{rigblock}.LootRigblock");
	}

    private ulong hashOfPrefix(ulong prefix) {
		if (!(prefix >= 1 && prefix <= 338)) {
			prefix = 0;
		}
		return fnv1aHashOfString($"_Generated/LootPrefix{prefix}.LootPrefix");
	}

	private ulong hashOfSuffix(ulong suffix) {
		if (!(suffix >= 1 && suffix <= 83) && !(suffix >= 10001 && suffix <= 10275)) {
			suffix = 0;
		}
		return fnv1aHashOfString($"_Generated/LootSuffix{suffix}.LootSuffix");
	}

    public CreaturePart addCreaturePart(Account account, CreaturePartTemplateModel creaturePartTemplate) {
        var part = new CreaturePart{
            AccountId = account.Id,
            CreationDate = 0, // TODO: current timestamp?

            Cost = creaturePartTemplate.cost,
            Level = creaturePartTemplate.level,

            Rarity = (CreaturePartRarity)creaturePartTemplate.rarity,
            MarketStatus = creaturePartTemplate.marketStatus,
            Status = creaturePartTemplate.status,
            Usage = creaturePartTemplate.usage,

            IsFlair = false, // TODO: what is that again?

            RigblockAssetHash = hashOfRigblock(creaturePartTemplate.rigblockAssetId),
            PrefixAssetHash = hashOfPrefix(creaturePartTemplate.prefixAssetId),
            PrefixSecondaryAssetHash = hashOfPrefix(creaturePartTemplate.prefixSecondaryAssetId),
            SuffixAssetHash = hashOfSuffix(creaturePartTemplate.suffixAssetId),

            RigblockAssetId = creaturePartTemplate.rigblockAssetId,
            PrefixAssetId = creaturePartTemplate.prefixAssetId,
            PrefixSecondaryAssetId = creaturePartTemplate.prefixSecondaryAssetId,
            SuffixAssetId = creaturePartTemplate.suffixAssetId
        };
        creaturePartRepository.insertCreaturePart(part);
        return part;
    }
}