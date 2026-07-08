using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Server.Util.Logging;
using Serilog.Events;
using Xunit.Abstractions;

namespace ReCap.Tests.Scripting;

// D-025 object death path. When combat drives an object's HP to <=0 the engine must remove it from
// the world (server-side, killing the corpse re-target loop) and tell the client to despawn it via
// ObjectDelete 0x8E. OnObjectDeath is the single death sink fired from the HP-mutation point.
public class DeathPathTests(ITestOutputHelper output)
{
    private const uint PummelHash = 0x5A0C3595;

    [Fact]
    public void OnObjectDeath_Removes_Object_From_World()
    {
        var game = new Game(1, GameType.Matched, null);
        var enemy = game.Objects.Spawn(10, 0, new Vector3(0, 0, 0), 1f, 2, false);
        enemy.Health = 0f;
        enemy.MaxHealth = 18f;

        game.OnObjectDeath(10);

        Assert.False(game.Objects.Objects.ContainsKey(10),
            "dead object must be removed from the world so it stops being a valid target");
    }

    [Fact]
    public void OnObjectDeath_Is_Idempotent_On_Missing_Object()
    {
        var game = new Game(1, GameType.Matched, null);
        game.OnObjectDeath(999); // never spawned — must not throw
    }

    [Fact]
    public void ApplyHeal_To_Zero_Hp_Kills_And_Despawns_The_Enemy()
    {
        var dataDir = ScriptEngineBootTests.FindDataDirShared();
        if (dataDir is null) return; // native lua dll / package data not present (CI)

        LoggingConfig.Bootstrap(LogEventLevel.Debug);

        var vfs = new ScriptVfs(new PackageMounts(dataDir));
        var engine = new ScriptEngine(vfs);
        var game = new Game(1, GameType.Matched, null);
        using var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;

        var enemy = game.Objects.Spawn(10, 0, new Vector3(0, 1.2f, 0), 1f, 2, false);
        enemy.Health = 6f; enemy.MaxHealth = 18f;

        var dealt = ctx.ApplyHeal(10, -enemy.Health); // negative = damage; drive HP to 0

        output.WriteLine($"dealt={dealt} dead={enemy.Dead} stillInWorld={game.Objects.Objects.ContainsKey(10)}");
        Assert.True(enemy.Dead, "object that reached 0 hp must be flagged dead");
        Assert.False(game.Objects.Objects.ContainsKey(10), "dead enemy must be despawned");
    }

    // Full pipeline: cast Pummel at a low-hp enemy until weapon damage kills it, then the corpse is
    // gone so the next cast finds no target (no infinite no-op cast loop on the corpse — D-025 root).
    [Fact]
    public void Pummel_Kills_Low_Hp_Enemy_Then_Corpse_Is_Gone()
    {
        var dataDir = ScriptEngineBootTests.FindDataDirShared();
        if (dataDir is null) return;

        LoggingConfig.Bootstrap(LogEventLevel.Debug);

        var vfs = new ScriptVfs(new PackageMounts(dataDir));
        var engine = new ScriptEngine(vfs);
        var game = new Game(1, GameType.Matched, null);
        using var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;

        var hero = game.Objects.Spawn(2, 0, new Vector3(0, 0, 0), 1f, 1, true);
        hero.Health = 375f; hero.MaxHealth = 375f;
        var enemy = game.Objects.Spawn(10, 0, new Vector3(0, 1.2f, 0), 1f, 2, false);
        enemy.Health = 1f; enemy.MaxHealth = 18f; // 1 hp vs min weapon roll 1 → one hit always kills

        for (var i = 0; i < 530; i++) ctx.Tick();

        ctx.InvokeAbility(PummelHash, 2, 10, 0f, 1.2f, 0f, 0);
        for (var tick = 0; tick < 200; tick++)
        {
            ctx.Tick();
            if (!ctx.Scheduler.HasThreadForObject(2)) break;
        }

        output.WriteLine($"enemyAlive={game.Objects.Objects.ContainsKey(10)} errors={ctx.Scheduler.ErrorCount}");
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
        Assert.False(game.Objects.Objects.ContainsKey(10),
            "Pummel should have killed the enemy and the corpse should be despawned");
    }
}
