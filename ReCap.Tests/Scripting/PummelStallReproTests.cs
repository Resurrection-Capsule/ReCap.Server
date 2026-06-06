using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Server.Util.Logging;
using Serilog.Events;
using Xunit.Abstractions;

namespace ReCap.Tests.Scripting;

// In-game stall repro (2026-06-06 14:27 session): Pummel invoked once, thread stayed busy 6s+,
// no HealDamage, no coroutine error. Mirrors the production wiring (real Game + GameScriptContext)
// with the in-game timeline: clock runs ~26s before the cast, target hostile and in melee range.
public class PummelStallReproTests(ITestOutputHelper output)
{
    private const uint PummelHash = 0x5A0C3595;

    [Fact]
    public void PummelTickCompletesAfterInvoke()
    {
        var dataDir = ScriptEngineBootTests.FindDataDirShared();
        if (dataDir is null) return;

        LoggingConfig.Bootstrap(LogEventLevel.Debug);

        var vfs = new ScriptVfs(new PackageMounts(dataDir));
        var engine = new ScriptEngine(vfs);
        var game = new Game(1, GameType.Matched, null);
        using var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;

        var hero = game.Objects.Spawn(2, 0, new System.Numerics.Vector3(0, 0, 0), 1f, 1, true);
        hero.Health = 375f; hero.MaxHealth = 375f;
        var enemy = game.Objects.Spawn(10, 0, new System.Numerics.Vector3(0, 1.2f, 0), 1f, 2, false);
        enemy.Health = 375f; enemy.MaxHealth = 375f;

        // In-game the context ticked ~26s before the cast — replicate the clock state.
        for (var i = 0; i < 530; i++) ctx.Tick();

        var invoked = ctx.InvokeAbility(PummelHash, 2, 10, 0f, 1.2f, 0f, 0);
        output.WriteLine($"invoked={invoked} busyAfterInvoke={ctx.Scheduler.HasThreadForObject(2)}");
        Assert.True(invoked);

        var ticksUsed = -1;
        for (var tick = 0; tick < 200; tick++)
        {
            ctx.Tick();
            if (!ctx.Scheduler.HasThreadForObject(2)) { ticksUsed = tick + 1; break; }
        }

        output.WriteLine($"ticksUsed={ticksUsed} errors={ctx.Scheduler.ErrorCount} enemyHp={enemy.Health}");
        Assert.True(ticksUsed >= 0, "thread never completed within 200 ticks (10s sim) — in-game stall reproduced");
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
    }
}
