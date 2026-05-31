using AutoMapper;
using ReCap.Server.Adapters.Rest.Contracts.Game;
using ReCap.Server.Adapters.Rest.Contracts.Game.Models;
using ReCap.Server.Config;
using ReCap.Server.Domain;
using ReCap.Server.Models;

namespace ReCap.Server.Mappers;

public class CreatureMapper
{
    private IMapper mapper;

    public CreatureMapper() {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Creature, CreatureModel>();
            cfg.CreateMap<CreatureModel, CreatureContract>();
            cfg.CreateMap<CreatureTemplateModel, GetCreatureResponseContract>();
        });
        mapper = configuration.CreateMapper();
    }

    public CreatureModel toModel(Creature creature) {
        return mapper.Map<CreatureModel>(creature);
    }

    // C++ stores png_thumb_url empty (profile XML has no such node); the client itself builds
    // /template_png/<id>_thumb.png from the creature and fetches it via its registered content
    // route. Injecting an absolute http://localhost/... url (as a prior attempt did) overrides
    // that and the client never fetches. We emit the exact root-relative path the client requests
    // in the C++ log, served from resources/static/template_png/ at /template_png/.
    public static string CreaturePngUrl(ulong templateId)
        => $"/template_png/{templateId}_thumb.png";

    public CreatureContract toContract(CreatureModel creature) {
        var contract = mapper.Map<CreatureContract>(creature);
        contract.LargePngUrl = CreaturePngUrl(creature.TemplateID);
        contract.ThumbPngUrl = CreaturePngUrl(creature.TemplateID);
        return contract;
    }

    public GetCreatureResponseContract toGetCreatureContract(CreatureTemplateModel creatureTemplate, CreatureModel creature, bool includeAbilities, bool includeParts) {
        var response = mapper.Map<GetCreatureResponseContract>(creatureTemplate);
        response.Stat = "ok";
        response.Timestamp = 1;
        response.ExecTime = 1;

        response.PartsStr = creature.getPartsAsString();
        response.StatsTemplate = null;

        response.ID = creature.ID;
        response.AccountID = creature.AccountID;
        response.Version = creature.Version;
        response.TemplateID = creature.TemplateID;
        response.GearScore = creature.GearScore;
        response.ItemPoints = creature.ItemPoints;
        response.LargePngUrl = CreaturePngUrl(creature.TemplateID);
        response.ThumbPngUrl = CreaturePngUrl(creature.TemplateID);
        response.Stats = creature.getStatsAsString();

        return response;
    }
}