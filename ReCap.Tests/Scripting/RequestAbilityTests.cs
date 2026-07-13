using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Adapters.Scripting.Api;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// nAbility.RequestAbility (Ghidra @0x00a42bf0): runs an ability programmatically. Server-side the
// native enqueues on the bridge and the request is drained at the top of the next Tick, so it spawns
// on the game loop instead of re-entering the runtime from inside the requesting coroutine.
public class RequestAbilityTests
{
    [Fact]
    public void RequestAbility_DeferredThenSpawnsTickCoroutineForAgent()
    {
        var dataDir = ScriptEngineBootTests.FindDataDirShared();
        if (dataDir is null) return;

        StubTelemetry.Clear();
        var vfs = new ScriptVfs(new PackageMounts(dataDir));
        var engine = new ScriptEngine(vfs);
        var game = new Game(1, GameType.Matched, null);
        using var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;

        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 1, true);
        game.Objects.Spawn(20, 0, new Vector3(5, 0, 0), 1f, 2, false);

        // Synthetic ability whose tick parks, so a successful cast leaves a live coroutine to observe.
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nAbility.RegisterAbility("ReqSpawnTest", {
                tick = function() nThread.WaitUntilTime(1) end
            })
            """), "reg");

        // Lua-driven: SPID boxes the name→hash the same way the corpus passes ability guids, so
        // ResolveAssetHash round-trips it back to the registered ability.
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nAbility.RequestAbility(nUtil.SPID("ReqSpawnTest"), 10, 20, 0, 0, 0, 0)
            """), "req");

        Assert.False(ctx.Scheduler.HasThreadForObject(10)); // deferred: nothing spawns synchronously
        ctx.Tick();                                          // queue drained -> InvokeAbility runs
        Assert.True(ctx.Scheduler.HasThreadForObject(10));   // ability tick coroutine now parked
    }

    [Fact]
    public void RequestAbility_UnknownAbilityIsHarmless()
    {
        var dataDir = ScriptEngineBootTests.FindDataDirShared();
        if (dataDir is null) return;

        var vfs = new ScriptVfs(new PackageMounts(dataDir));
        var engine = new ScriptEngine(vfs);
        var game = new Game(1, GameType.Matched, null);
        using var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 1, true);

        ctx.RequestAbility(0xDEADBEEF, 10, 0, 0, 0, 0, 0);
        ctx.Tick(); // InvokeAbility finds no entry -> no throw, no thread
        Assert.False(ctx.Scheduler.HasThreadForObject(10));
    }
}
