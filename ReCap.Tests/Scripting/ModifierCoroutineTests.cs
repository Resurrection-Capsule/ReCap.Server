using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Adapters.Scripting.Api;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;

namespace ReCap.Tests.Scripting;

// Integration: RequestModifier (bridge CreateModifier) spawns the modifier's index [2] as a persistent
// per-instance coroutine (retail FUN_009e5c50), and RemoveModifier stops it + runs [3]. Gated on the
// game data dir like AbilityTickGateTests (skips silently when unavailable).
public class ModifierCoroutineTests
{
    [Fact]
    public void RealModifiersCreateTickCoroutineAndRemoveCleanly()
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

        var modifiers = ctx.Registry.AllEntries(ScriptKind.Modifier).Take(40).ToList();
        Assert.NotEmpty(modifiers);

        var liveTickThreads = 0;
        var createdAndRemoved = 0;

        foreach (var entry in modifiers)
        {
            var id = ctx.CreateModifier(10, 20, entry.Hash, 0);
            Assert.NotEqual(0u, id);

            var instance = game.Modifiers.Get(id);
            Assert.NotNull(instance);

            // A modifier with a numeric [2] tick that yields (DoT/aura loop) leaves a live thread.
            if (instance!.ThreadHandle != 0 && ctx.Scheduler.HasThread(instance.ThreadHandle))
            {
                liveTickThreads++;
                for (var t = 0; t < 3; t++) ctx.Tick(); // let it run a few frames, must not blow up
            }

            Assert.True(ctx.RemoveModifier(id));
            Assert.Null(game.Modifiers.Get(id));
            if (instance.ThreadHandle != 0)
                Assert.False(ctx.Scheduler.HasThread(instance.ThreadHandle)); // tick thread stopped
            createdAndRemoved++;
        }

        Console.WriteLine($"Modifiers: batch={modifiers.Count} created+removed={createdAndRemoved} liveTick={liveTickThreads}");
        Assert.Equal(modifiers.Count, createdAndRemoved);
        Assert.True(liveTickThreads >= 1, $"expected >=1 modifier to spawn a live [2] tick coroutine, got {liveTickThreads}");
    }
}
