using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Adapters.Scripting.Api;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;

namespace ReCap.Tests.Scripting;

public class AbilityTickGateTests
{
    [Fact]
    public void RealRetailAbilityTickRunsToCompletion()
    {
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

        var candidates = ctx.Registry.AllWithTick(ScriptKind.Ability).Take(25).ToList();
        Assert.NotEmpty(candidates);

        var started = 0;
        var completed = 0;
        var skippedGuard = 0;

        foreach (var entry in candidates)
        {
            var invoked = ctx.InvokeAbility(entry.Hash, 10, 20, 5f, 0f, 0f, 1);
            if (!invoked) { skippedGuard++; continue; }
            started++;

            for (var tick = 0; tick < 600 && ctx.Scheduler.HasThreadForObject(10); tick++)
                ctx.Tick();

            if (!ctx.Scheduler.HasThreadForObject(10))
                completed++;
        }

        Console.WriteLine($"Gate: candidates={candidates.Count} started={started} completed={completed} skipped(guard)={skippedGuard}");

        var telemetry = StubTelemetry.Snapshot().Take(20).ToList();
        Console.WriteLine($"Stub telemetry top-{telemetry.Count} (P4 backlog):");
        foreach (var t in telemetry)
            Console.WriteLine($"  {t}");

        Assert.True(completed >= 5,
            $"expected >=5 retail ability ticks to complete; got {completed}/{started} started (guard-skipped={skippedGuard})");
    }
}
