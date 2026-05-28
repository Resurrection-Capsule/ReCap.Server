using AssetData.Parser.Model;

namespace ReCap.Server.Services;

public sealed class AbilityRegistry(AssetDatabase? assets)
{
    public AssetValue? GetAbility(uint id) => assets?.GetAbility(id);
    public AssetValue? GetAbilityByName(string name) => assets?.GetAbilityByName(name);
}
