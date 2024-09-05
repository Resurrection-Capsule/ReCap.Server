using HttpServer;

namespace HttpServer;

public class CreatureTemplateModel
{
    public ulong id { get; set; }

    public string nameLocaleId { get; set; }
    public string descLocaleId { get; set; }

    public string name { get; set; }
    public string elementType { get; set; }

    public double weaponMinDamage = 0;
    public double weaponMaxDamage = 0;
    public double gearScore = 0;

    public string classType { get; set; }

    public string statsTemplate { get; set; }
    public string statsTemplateAbility { get; set; }
    public string statsTemplateAbilityKeyvalues { get; set; }

    public bool hasHands { get; set; }
    public bool hasFeet { get; set; }

    public int abilityPassive { get; set; }
    public int abilityBasic { get; set; }
    public int abilityRandom { get; set; }
    public int abilitySpecial1 { get; set; }
    public int abilitySpecial2 { get; set; }
}
