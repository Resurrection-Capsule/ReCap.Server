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

    // A player who rendered a custom portrait (updateCreature stored the PNG in the DB) gets a
    // per-creature URL served from the blob by PngStorageAdapter; otherwise the shared per-template
    // thumbnail on disk. Either path resolves — the adapter serves both.
    private static string ThumbUrl(CreatureModel c)
        => string.IsNullOrEmpty(c.ThumbPngBase64) ? $"/template_png/{c.TemplateID}_thumb.png" : $"/creature_png/{c.ID}_thumb.png";

    private static string LargeUrl(CreatureModel c)
        => string.IsNullOrEmpty(c.LargePngBase64) ? $"/template_png/{c.TemplateID}_thumb.png" : $"/creature_png/{c.ID}_large.png";

    public CreatureContract toContract(CreatureModel creature) => new()
    {
        ID = creature.ID,
        Version = creature.Version,
        TemplateID = creature.TemplateID,
        TemplateName = creature.TemplateName,
        GearScore = creature.GearScore,
        ItemPoints = creature.ItemPoints,
        LargePngUrl = LargeUrl(creature),
        ThumbPngUrl = ThumbUrl(creature),
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
        LargePngUrl = LargeUrl(creature),
        ThumbPngUrl = ThumbUrl(creature),
        Stats = creature.getStatsAsString(),
    };

    // api.creature.getTemplate — template-level data only (no per-creature parts/gear).
    // Fields per the client contract (TemplateCreature::WriteApi); type_a = elementType.
    public GetCreatureTemplateResponseContract toGetCreatureTemplateContract(CreatureTemplateModel t) => new()
    {
        Stat = "ok",
        Timestamp = 1,
        ExecTime = 1,

        NameLocaleId = t.nameLocaleId,
        TextLocaleId = t.descLocaleId,
        TemplateName = t.name,
        Type = t.elementType,
        WeaponMinDamage = t.weaponMinDamage,
        WeaponMaxDamage = t.weaponMaxDamage,
        GearScore = t.gearScore,
        Class = t.classType,
        StatsTemplate = t.statsTemplate,

        AbilityBasic = (ulong)t.abilityBasic,
        AbilitySpecial1 = (ulong)t.abilitySpecial1,
        AbilitySpecial2 = (ulong)t.abilitySpecial2,
        AbilityRandom = (ulong)t.abilityRandom,
        AbilityPassive = (ulong)t.abilityPassive,
    };
}
