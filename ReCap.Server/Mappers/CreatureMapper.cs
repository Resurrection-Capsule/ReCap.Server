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

    public static string CreaturePngUrl(ulong creatureId, string method)
        => $"http://{ServerConfig.HostName}/recap/api?method={method}&id={creatureId}";

    public CreatureContract toContract(CreatureModel creature) {
        var contract = mapper.Map<CreatureContract>(creature);
        // Always expose image urls; the endpoint falls back to the template png when the
        // creature has no custom (edited) png. Deck/squad cards need these or the in-game
        // deck HUD fails to bind and crashes.
        contract.LargePngUrl = CreaturePngUrl(creature.ID, "api.game.getCreatureLargePng");
        contract.ThumbPngUrl = CreaturePngUrl(creature.ID, "api.game.getCreatureThumbPng");
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
        response.LargePngUrl = CreaturePngUrl(creature.ID, "api.game.getCreatureLargePng");
        response.ThumbPngUrl = CreaturePngUrl(creature.ID, "api.game.getCreatureThumbPng");
        response.Stats = creature.getStatsAsString();

        return response;
    }
}