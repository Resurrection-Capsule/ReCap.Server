using ReCap.Server.Adapters.Rest.Contracts.Game;
using ReCap.Server.Models;

namespace ReCap.Server.Mappers;

public class CreatureTemplateMapper
{
    public GetCreatureTemplateResponseContract toContract(CreatureTemplateModel creatureTemplate) => new()
    {
        NameLocaleId = creatureTemplate.nameLocaleId,
        WeaponMinDamage = creatureTemplate.weaponMinDamage,
        WeaponMaxDamage = creatureTemplate.weaponMaxDamage,
        GearScore = creatureTemplate.gearScore,
        StatsTemplate = creatureTemplate.statsTemplate,
        AbilityPassive = (ulong)creatureTemplate.abilityPassive,
        AbilityBasic = (ulong)creatureTemplate.abilityBasic,
        AbilityRandom = (ulong)creatureTemplate.abilityRandom,
        AbilitySpecial1 = (ulong)creatureTemplate.abilitySpecial1,
        AbilitySpecial2 = (ulong)creatureTemplate.abilitySpecial2,
    };
}
