using ReCap.Server.Adapters.Rest.Contracts.Game;
using ReCap.Server.Adapters.Rest.Contracts.Game.Models;
using ReCap.Server.Domain;
using ReCap.Server.Models;

namespace ReCap.Server.Mappers;

public class CreatureMapper
{
    public CreatureModel toModel(Creature creature) => new()
    {
        ID = creature.ID,
        Version = creature.Version,
        TemplateID = creature.TemplateID,
        TemplateName = creature.TemplateName,
        AccountID = creature.AccountID,
        Cost = creature.Cost,
        GearScore = creature.GearScore,
        ItemPoints = creature.ItemPoints,
        LargePngUrl = creature.LargePngUrl,
        ThumbPngUrl = creature.ThumbPngUrl,
    };

    // C++ stores png_thumb_url empty (profile XML has no such node); the client itself builds
    // /template_png/<id>_thumb.png from the creature and fetches it via its registered content
    // route. Injecting an absolute http://localhost/... url (as a prior attempt did) overrides
    // that and the client never fetches. We emit the exact root-relative path the client requests
    // in the C++ log, served from resources/static/template_png/ at /template_png/.
    public static string CreaturePngUrl(ulong templateId)
        => $"/template_png/{templateId}_thumb.png";

    public CreatureContract toContract(CreatureModel creature) => new()
    {
        ID = creature.ID,
        Version = creature.Version,
        TemplateID = creature.TemplateID,
        TemplateName = creature.TemplateName,
        GearScore = creature.GearScore,
        ItemPoints = creature.ItemPoints,
        LargePngUrl = CreaturePngUrl(creature.TemplateID),
        ThumbPngUrl = CreaturePngUrl(creature.TemplateID),
    };

    public GetCreatureResponseContract toGetCreatureContract(CreatureTemplateModel creatureTemplate, CreatureModel creature, bool includeAbilities, bool includeParts) => new()
    {
        Stat = "ok",
        Timestamp = 1,
        ExecTime = 1,

        NameLocaleId = creatureTemplate.nameLocaleId,
        WeaponMinDamage = creatureTemplate.weaponMinDamage,
        WeaponMaxDamage = creatureTemplate.weaponMaxDamage,
        AbilityPassive = (ulong)creatureTemplate.abilityPassive,
        AbilityBasic = (ulong)creatureTemplate.abilityBasic,
        AbilityRandom = (ulong)creatureTemplate.abilityRandom,
        AbilitySpecial1 = (ulong)creatureTemplate.abilitySpecial1,
        AbilitySpecial2 = (ulong)creatureTemplate.abilitySpecial2,

        PartsStr = creature.getPartsAsString(),
        StatsTemplate = null,

        ID = creature.ID,
        AccountID = creature.AccountID,
        Version = creature.Version,
        TemplateID = creature.TemplateID,
        GearScore = creature.GearScore,
        ItemPoints = creature.ItemPoints,
        LargePngUrl = CreaturePngUrl(creature.TemplateID),
        ThumbPngUrl = CreaturePngUrl(creature.TemplateID),
        Stats = creature.getStatsAsString(),
    };
}
