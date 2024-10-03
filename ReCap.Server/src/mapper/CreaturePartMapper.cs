namespace ReCap.Server.Mapper.CreaturePart;

using AutoMapper;

using HttpServer;

using ReCap.Server.Adapters.Rest.Contracts.Game.Models.CreaturePart;
using ReCap.Server.Domain.CreaturePart;
using ReCap.Server.Enums.CreaturePartRarity;
using ReCap.Server.Model.CreaturePart;
using ReCap.Server.Model.CreaturePartTemplate;

public class CreaturePartMapper
{
    private IMapper mapper;

    public CreaturePartMapper() {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CreaturePart, CreaturePartModel>();
            cfg.CreateMap<CreaturePartModel, CreaturePartContract>();
        });
        mapper = configuration.CreateMapper();
    }

    public CreaturePartModel toModel(CreaturePart creaturePart) {
        return mapper.Map<CreaturePartModel>(creaturePart);
    }

    public CreaturePartContract toContract(CreaturePartModel creaturePart) {
        var contract = mapper.Map<CreaturePartContract>(creaturePart);
        contract.ReferenceID = contract.ID;
        return contract;
    }

    private ulong fnv1aHashOfString(string val) {
        ulong h = 0x811c9dc5u;

        int size = val.Length;
        for (int i = 0; i < size; i++)
        {
            h *= 0x01000193u;
            h ^= val[i];
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