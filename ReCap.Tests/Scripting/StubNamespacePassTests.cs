using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// The demanded stub-namespace pass (Ghidra 2026-07-13): nPhysics (flags/LOS/terrain approximations),
// nClient (DrawReticle no-ops — the retail client stubs these too), nGameDirector.GetKillPercent.
public class StubNamespacePassTests
{
    private static GameScriptContext MakeContext(out Game game)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "recap-stubpass-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(tempDir)));
        game = new Game(1, GameType.Matched, null);
        var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        return ctx;
    }

    [Fact]
    public void Physics_LosClear_TerrainZero_IsDynamicFalse_NoOpsSafe()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nPhysics.IsInLineOfSight(10, 20) == true")));
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nPhysics.DistanceToTerrain(0,0,0, 0,-1,0, 100) == 0")));
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nPhysics.IsDynamic(10) == false")));
        // no-ops + collidable/force-update must run without error
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nPhysics.AddPhysicsForObject(10)
            nPhysics.RemovePhysicsForObject(10)
            nPhysics.ApplyImpulse(10, 1, 0, 0)
            nPhysics.SetObjectAsCollidable(10, false)
            nPhysics.ForceClientUpdate(10)
            """), "phys");
        Assert.True(game.Objects.Objects[10].NavCollisionDisabled); // collidable=false -> nav disabled
    }

    [Fact]
    public void Client_DrawReticle_AreHarmlessNoOps()
    {
        using var ctx = MakeContext(out _);
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nClient.DrawReticle_Circle(0,0,0, 5)
            nClient.DrawReticle_Cone(0,0,0, 1,0,0, 5, 1.0)
            nClient.DrawReticle_PointBlankCircle(0,0,0, 3)
            """), "reticle");
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return true")));
    }

    [Fact]
    public void GameDirector_GetKillPercent_TracksCombatantDeaths()
    {
        using var ctx = MakeContext(out var game);
        // No enemies spawned yet -> 0.
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nGameDirector.GetKillPercent() == 0")));

        // Simulate two combatant enemies spawned + one killed via the death path.
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);
        game.Objects.Objects[10].MaxHealth = 50f;
        game.Objects.Spawn(11, 0, new Vector3(5, 0, 0), 1f, 2, false);
        game.Objects.Objects[11].MaxHealth = 50f;
        // SpawnWorldObject increments the spawn counter in-game; here we drive the kill side directly.
        typeof(Game).GetField("_enemiesSpawned", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(game, 2);

        game.OnObjectDeath(10); // one combatant enemy defeated
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return math.abs(nGameDirector.GetKillPercent() - 0.5) < 0.001")));
    }
}
