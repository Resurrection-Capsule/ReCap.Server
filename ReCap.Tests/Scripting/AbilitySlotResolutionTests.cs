using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Services;
using ReCap.Server.Services.Assets;
using ReCap.Server.Services.Scripting;
using Xunit.Abstractions;

namespace ReCap.Tests.Scripting;

// Cast wiring (0x9C types 7/8): the PlayerClass ability slot fields resolve to FNV hashes
// that must exist in the boot-populated ScriptRegistry — the same lookup chain the client
// uses (PlayerClass field -> FNV -> ability registry, Ghidra FUN_009c7180 + RegisterAbility).
public class AbilitySlotResolutionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PlayerClassSlotsResolveToRegisteredAbilities()
    {
        var dataDir = ScriptEngineBootTests.FindDataDirShared();
        if (dataDir is null) return;

        using var assets = new AssetDatabase(Path.Combine(dataDir, "AssetData_Binary.package"));
        await assets.WarmUpAsync();

        var vfs = new ScriptVfs(new PackageMounts(dataDir));
        var engine = new ScriptEngine(vfs);
        using var rt = LuaRuntime.CreateSandboxedState(n => vfs.GetChunk(ScriptVfs.ParseReference(n)));
        engine.ExecuteBootScripts(rt);
        var registry = ScriptContextRegistry.Get(rt.L)!.Registry;

        var heroNouns = assets.Nouns
            .Where(kv => (kv.Value.FindByName("playerClassData") as AssetData.Parser.Model.StringValue)?.Value is { Length: > 0 })
            .Select(kv => kv.Key)
            .ToList();
        Assert.True(heroNouns.Count > 0, "no hero nouns with playerClassData found");
        output.WriteLine($"hero nouns: {heroNouns.Count}");

        var resolvedHeroes = 0;
        var registeredBasics = 0;
        var tickableBasics = 0;
        foreach (var nounId in heroNouns)
        {
            var slots = assets.ResolveAbilitySlots(nounId);
            if (slots is null) continue;
            Assert.Equal(5, slots.Length);
            if (slots[0] == 0) continue;
            resolvedHeroes++;

            var basic = registry.Find(ScriptKind.Ability, slots[0]);
            if (basic is not null)
            {
                registeredBasics++;
                if (basic.HasTick) tickableBasics++;
            }
        }

        output.WriteLine($"resolved={resolvedHeroes} basicsInRegistry={registeredBasics} tickableBasics={tickableBasics}");
        Assert.True(resolvedHeroes >= 50, $"expected >=50 heroes with a basic ability slot, got {resolvedHeroes}");
        Assert.True(registeredBasics >= resolvedHeroes / 2,
            $"PlayerClass basicAbility names must land in the RegisterAbility registry: {registeredBasics}/{resolvedHeroes}");
        Assert.True(tickableBasics > 0, "at least one basic ability must be tick-capable");
    }
}
