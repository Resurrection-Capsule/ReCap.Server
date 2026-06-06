using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Server.Util.Logging;
using Serilog.Events;
using Xunit.Abstractions;

namespace ReCap.Tests.Scripting;

// Diagnostic probe: per-ability error attribution for the 25-ability gate set.
public class GateErrorProbeTests(ITestOutputHelper output)
{
    [Fact]
    public void DumpPerAbilityGateErrors()
    {
        var dataDir = ScriptEngineBootTests.FindDataDirShared();
        if (dataDir is null) return;

        LoggingConfig.Bootstrap(LogEventLevel.Debug);

        var vfs = new ScriptVfs(new PackageMounts(dataDir));
        var engine = new ScriptEngine(vfs);
        var game = new Game(1, GameType.Matched, null);
        using var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;

        game.Objects.Spawn(10, 0, new System.Numerics.Vector3(0, 0, 0), 1f, 1, true);
        game.Objects.Spawn(20, 0, new System.Numerics.Vector3(5, 0, 0), 1f, 2, false);

        foreach (var entry in ctx.Registry.AllWithTick(ScriptKind.Ability).Take(25))
        {
            var before = ctx.Scheduler.ErrorCount;
            var invoked = ctx.InvokeAbility(entry.Hash, 10, 20, 5f, 0f, 0f, 1);
            if (!invoked) { output.WriteLine($"{entry.Name}: NOT INVOKED"); continue; }
            for (var tick = 0; tick < 600 && ctx.Scheduler.HasThreadForObject(10); tick++)
                ctx.Tick();
            var errored = ctx.Scheduler.ErrorCount - before;
            output.WriteLine($"{entry.Name}: {(errored > 0 ? "ERRORED" : "clean")}");
        }
        Assert.True(true);
    }
}
