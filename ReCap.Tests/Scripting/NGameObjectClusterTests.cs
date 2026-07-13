using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Domain.Gameplay.AI;
using ReCap.Server.Services.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// Tier-2 nGameObject cluster (Ghidra 2026-07-13): GetObjectDistance@0x00a060c0, Get/SetOwnerID
// @0x009fd910/0x009fd950, SetTargetID@0x009fce30, SetOrientation@0x00a081c0, AddAggroForObject
// @0x009fd690, AlertObject@0x009fd750. Driven through the real GameScriptContext.
public class NGameObjectClusterTests
{
    private static GameScriptContext MakeContext(out Game game)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "recap-ngo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(tempDir)));
        game = new Game(1, GameType.Matched, null);
        var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        return ctx;
    }

    [Fact]
    public void GetObjectDistance_IsEdgeToEdgeFlooredAtZero()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);          // scale 1
        game.Objects.Spawn(20, 0, new Vector3(10, 0, 0), 1f, 1, true);  // scale 1, 10 apart

        // center 10 - (1 + 1) radii = 8
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nGameObject.GetObjectDistance(10, 20) == 8")));

        game.Objects.Objects[20].Position = new Vector3(1, 0, 0); // overlapping -> floored at 0
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nGameObject.GetObjectDistance(10, 20) == 0")));
    }

    [Fact]
    public void OwnerId_RoundTrips()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);

        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.SetOwnerID(10, 42)"), "so");
        Assert.Equal(42u, game.Objects.Objects[10].OwnerId);
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nGameObject.GetOwnerID(10) == 42")));
    }

    [Fact]
    public void SetTargetID_SetsAndClears()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);

        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.SetTargetID(10, 20)"), "st");
        Assert.Equal(20u, game.Objects.Objects[10].TargetId);
        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.SetTargetID(10, 0)"), "st0"); // kObjIDNone
        Assert.Equal(0u, game.Objects.Objects[10].TargetId);
    }

    [Fact]
    public void SetOrientation_FullQuaternionAndYawZero()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);

        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.SetOrientation(10, 0, 0.707, 0, 0.707)"), "q");
        var q = game.Objects.Objects[10].Orientation;
        Assert.Equal(0.707f, q.Y, 3);
        Assert.Equal(0.707f, q.W, 3);

        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.SetOrientation(10, 0)"), "yaw0"); // reset
        Assert.Equal(new Quaternion(0, 0, 0, 1), game.Objects.Objects[10].Orientation);
    }

    [Fact]
    public void AddAggroForObject_And_AlertObject_RaiseAgentThreat()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);
        game.Objects.Objects[10].Agent = new AgentBlackboard(); // an AI agent
        game.Objects.Spawn(20, 0, new Vector3(5, 0, 0), 1f, 1, true);

        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.AddAggroForObject(10, 20, 10, \"probe\")"), "aggro");
        var bb = game.Objects.Objects[10].Agent!;
        Assert.Contains(bb.AggroList, e => e.ObjectId == 20 && e.Threat == 10f);

        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.AlertObject(10, 30)"), "alert");
        Assert.Contains(bb.AggroList, e => e.ObjectId == 30); // noticed with no extra threat
    }

    [Fact]
    public void AddAggro_OnNonAgent_IsHarmless()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false); // no blackboard

        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.AddAggroForObject(10, 20, 5)"), "aggro");
        Assert.Null(game.Objects.Objects[10].Agent); // still not an agent, no crash
    }
}
