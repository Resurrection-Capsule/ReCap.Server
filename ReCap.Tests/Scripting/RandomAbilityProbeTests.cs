using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Adapters.Scripting.Native;
using ReCap.Server.Services.Scripting;
using Xunit.Abstractions;

namespace ReCap.Tests.Scripting;

// Probe: what does a "*Random" ability table (PlayerClass specialAbility2 family) contain?
// Drives the slot-2 cast design: NecroRandom resolved from the registry but was refused.
public class RandomAbilityProbeTests(ITestOutputHelper output)
{
    [Fact]
    public void DumpRandomAbilityTables()
    {
        var dataDir = ScriptEngineBootTests.FindDataDirShared();
        if (dataDir is null) return;

        var vfs = new ScriptVfs(new PackageMounts(dataDir));
        var engine = new ScriptEngine(vfs);
        using var rt = LuaRuntime.CreateSandboxedState(n => vfs.GetChunk(ScriptVfs.ParseReference(n)));
        engine.ExecuteBootScripts(rt);
        var registry = ScriptContextRegistry.Get(rt.L)!.Registry;

        string[] probes = ["NecroRandom", "PlasmaRandom", "PlasmaRandom_WebbedLightning", "TechRandom3", "LightningRogueBasic"];
        foreach (var name in probes)
        {
            var entry = registry.Find(ScriptKind.Ability, ScriptVfs.Hash(name));
            if (entry is null)
            {
                output.WriteLine($"{name}: NOT IN REGISTRY");
                continue;
            }
            output.WriteLine($"{name}: hash=0x{entry.Hash:X8} tick={entry.HasTick} activate={entry.HasActivate} deactivate={entry.HasDeactivate}");

            var L = rt.L;
            LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, entry.TableRef);
            LuaNative.lua_pushnil(L);
            while (LuaNative.lua_next(L, -2) != 0)
            {
                var keyType = LuaNative.lua_type(L, -2);
                var valType = LuaNative.lua_type(L, -1);
                string key = keyType switch
                {
                    4 => LuaNative.ToManagedString(L, -2) ?? "?",
                    3 => $"#{LuaNative.lua_tonumber(L, -2):G}",
                    _ => $"<{keyType}>"
                };
                string val = valType switch
                {
                    3 => LuaNative.lua_tonumber(L, -1).ToString("G"),
                    4 => $"\"{LuaNative.ToManagedString(L, -1)}\"",
                    5 => "<table>",
                    6 => "<function>",
                    _ => $"<type {valType}>"
                };
                output.WriteLine($"    [{key}] = {val}");
                LuaNative.lua_settop(L, LuaNative.lua_gettop(L) - 1);
            }
            LuaNative.lua_settop(L, 0);
        }

        // Contract anchors: concrete random variants are tick-capable; the generic channeled
        // pools (NecroRandom/PlasmaRandom) expose only the Class vtable (#2/#3) — their cast
        // dispatch is a pending contract (VERIFIED_FACTS C7 follow-up).
        Assert.True(registry.Find(ScriptKind.Ability, ScriptVfs.Hash("PlasmaRandom_WebbedLightning"))?.HasTick);
        Assert.False(registry.Find(ScriptKind.Ability, ScriptVfs.Hash("NecroRandom"))?.HasTick);
    }
}
