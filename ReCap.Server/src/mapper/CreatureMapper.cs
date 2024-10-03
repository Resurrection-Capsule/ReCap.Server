namespace ReCap.Server.Mapper.Creature;

using AutoMapper;

using HttpServer;

using ReCap.Server.Adapters.Rest.Contracts.Game.GetCreatureResponse;
using ReCap.Server.Domain.Creature;
using ReCap.Server.Model.Creature;
using ReCap.Server.Model.CreatureTemplate;

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

    public CreatureContract toContract(CreatureModel creature) {
        return mapper.Map<CreatureContract>(creature);
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
        response.LargePngUrl = creature.LargePngUrl;
        response.ThumbPngUrl = creature.ThumbPngUrl;
        response.Stats = creature.getStatsAsString();

        return response;
    }
}