using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Adapters.Scripting.Api;
using ReCap.Server.Adapters.Scripting.Native;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using Xunit.Abstractions;

namespace ReCap.Tests.Scripting;

// Demand harvest: invokes EVERY tick-capable retail ability + modifier and collects the
// stub-telemetry union — the real porting backlog, by namespace, instead of the blind
// 432-native surface. Run explicitly: set RECAP_HARVEST=1 (skipped otherwise to keep the
// suite fast).
public class NativeDemandHarvestTests(ITestOutputHelper output)
{
    [Fact]
    public void HarvestNativeDemandAcrossAllTickScripts()
    {
        if (Environment.GetEnvironmentVariable("RECAP_HARVEST") != "1") return;
        var dataDir = ScriptEngineBootTests.FindDataDirShared();
        if (dataDir is null) return;

        StubTelemetry.Clear();

        var vfs = new ScriptVfs(new PackageMounts(dataDir));
        var engine = new ScriptEngine(vfs);
        var game = new Game(1, GameType.Matched, null);
        using var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;

        game.Objects.Spawn(10, 0, new System.Numerics.Vector3(0, 0, 0), 1f, 1, true);
        game.Objects.Spawn(20, 0, new System.Numerics.Vector3(5, 0, 0), 1f, 2, false);

        var stats = new Dictionary<ScriptKind, (int Started, int Completed, int Errored)>();
        foreach (var kind in new[] { ScriptKind.Ability, ScriptKind.Modifier })
        {
            int started = 0, completed = 0, errored = 0;
            foreach (var entry in ctx.Registry.AllWithTick(kind))
            {
                // Drain the previous coroutine; WaitForever sleepers need an explicit wake.
                for (var drain = 0; drain < 600 && ctx.Scheduler.HasThreadForObject(10); drain++)
                    ctx.Tick();
                if (ctx.Scheduler.HasThreadForObject(10))
                {
                    ctx.Scheduler.WakeObject(10);
                    for (var drain = 0; drain < 60 && ctx.Scheduler.HasThreadForObject(10); drain++)
                        ctx.Tick();
                }
                if (ctx.Scheduler.HasThreadForObject(10))
                {
                    output.WriteLine($"{kind}: aborted at '{entry.Name}' (stuck coroutine)");
                    break;
                }

                var before = ctx.Scheduler.ErrorCount;
                if (!InvokeTick(ctx, entry.TableRef, agentId: 10, targetId: 20)) continue;
                started++;
                for (var tick = 0; tick < 600 && ctx.Scheduler.HasThreadForObject(10); tick++)
                    ctx.Tick();
                if (!ctx.Scheduler.HasThreadForObject(10)) completed++;
                if (ctx.Scheduler.ErrorCount > before) errored++;
            }
            stats[kind] = (started, completed, errored);
        }

        foreach (var (kind, s) in stats)
            output.WriteLine($"{kind}: started={s.Started} completed={s.Completed} errored={s.Errored}");

        var demanded = StubTelemetry.Snapshot()
            .Select(t => t.Split('|').Last())
            .Where(t => !t.EndsWith(":called"))
            .Distinct()
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        output.WriteLine($"--- demanded natives: {demanded.Count} ---");
        foreach (var group in demanded.GroupBy(d => d.Split('.')[0]).OrderByDescending(g => g.Count()))
        {
            output.WriteLine($"[{group.Count(),3}] {group.Key}");
            foreach (var fn in group)
                output.WriteLine($"      {fn}");
        }
        Assert.True(true);
    }

    private static bool InvokeTick(GameScriptContext ctx, int tableRef, uint agentId, uint targetId)
    {
        var L = ctx.Runtime.L;
        if (ctx.Scheduler.HasThreadForObject(agentId)) return false;
        LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, tableRef);
        LuaNative.lua_getfield(L, -1, "tick");
        if (LuaNative.lua_type(L, -1) != LuaNative.LUA_TFUNCTION)
        {
            LuaNative.lua_settop(L, 0);
            return false;
        }
        LuaNative.lua_insert(L, -2);
        LuaNative.lua_pushnumber(L, agentId);
        LuaNative.lua_pushnumber(L, targetId);
        LuaNative.lua_pushnumber(L, 5f);
        LuaNative.lua_pushnumber(L, 0f);
        LuaNative.lua_pushnumber(L, 0f);
        LuaNative.lua_pushnumber(L, 1f);
        var inv = new AbilityInvocation(agentId, targetId, 5f, 0f, 0f, 1);
        var threadL = ctx.Scheduler.SpawnFromStack(L, agentId, 8,
            beforeFirstResume: t => ScriptContextRegistry.Get(L)?.SetInvocation(t, inv));
        LuaNative.lua_settop(L, 0);
        return threadL != 0;
    }
}
