namespace ReCap.Server.Model.CreatureTemplate;

public class CreatureTemplateModel
{
    public required ulong id { get; set; }

    public required string nameLocaleId { get; set; }
    public required string descLocaleId { get; set; }

    public required string name { get; set; }
    public required string elementType { get; set; }

    public required double weaponMinDamage = 0;
    public required double weaponMaxDamage = 0;
    public required double gearScore = 0;

    public required string classType { get; set; }

    public required string statsTemplate { get; set; }
    public required string statsTemplateAbility { get; set; }
    public required string statsTemplateAbilityKeyvalues { get; set; }

    public required bool hasHands { get; set; }
    public required bool hasFeet { get; set; }

    public required int abilityPassive { get; set; }
    public required int abilityBasic { get; set; }
    public required int abilityRandom { get; set; }
    public required int abilitySpecial1 { get; set; }
    public required int abilitySpecial2 { get; set; }
}
