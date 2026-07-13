using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// Tier-2 nLocomotion movement natives (Ghidra 2026-07-13): TeleportObject@0x00a04590,
// MoveToObject@0x009fadc0, MoveToPointExact@0x00a048c0, TurnToFaceTargetObject@0x009fb130.
// Driven through the real GameScriptContext so the object state / dirty-flag routing is exercised.
public class NLocomotionModuleTests
{
    private static GameScriptContext MakeContext(out Game game)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "recap-loco-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(tempDir)));
        game = new Game(1, GameType.Matched, null);
        var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        return ctx;
    }

    [Fact]
    public void TeleportObject_RepositionsAndFlagsTeleportRoute()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);

        ctx.Runtime.Execute(LuaFixtures.Compile("nLocomotion.TeleportObject(10, 5, 0, 7, true)"), "tp");

        var o = game.Objects.Objects[10];
        Assert.Equal(new Vector3(5, 0, 7), o.Position);
        Assert.Equal(0x020u, o.GoalFlags & 0x020u);                 // teleport route -> ObjectTeleport 0x90
        Assert.True((o.DirtyFlags & ObjectDirtyFlags.Locomotion) != 0);
        Assert.Equal(new Vector3(5, 0, 7), o.Facing);               // face=true orients along the jump
    }

    [Fact]
    public void MoveToObject_AimsGoalAtTargetPosition_ReturnsHasLocomotion()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);
        game.Objects.Spawn(20, 0, new Vector3(9, 0, 3), 1f, 1, true);

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nLocomotion.MoveToObject(10, 20) == true")));

        var o = game.Objects.Objects[10];
        Assert.Equal(new Vector3(9, 0, 3), o.GoalPosition);
        Assert.Equal(0x001u, o.GoalFlags);                          // smooth-move route
    }

    [Fact]
    public void MoveToPointExact_SetsGoalWithNoStopDistance()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nLocomotion.MoveToPointExact(10, 4, 0, 6) == true")));

        var o = game.Objects.Objects[10];
        Assert.Equal(new Vector3(4, 0, 6), o.GoalPosition);
        Assert.Equal(0f, o.DesiredStopDistance);
    }

    [Fact]
    public void MoveToObject_MissingTargetReturnsFalse()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nLocomotion.MoveToObject(10, 999) == false")));
    }

    [Fact]
    public void TurnToFaceTargetObject_FacesAlongVectorToTarget()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, new Vector3(1, 0, 1), 1f, 2, false);
        game.Objects.Spawn(20, 0, new Vector3(4, 0, 5), 1f, 1, true);

        ctx.Runtime.Execute(LuaFixtures.Compile("nLocomotion.TurnToFaceTargetObject(10, 20)"), "face");

        Assert.Equal(new Vector3(3, 0, 4), game.Objects.Objects[10].Facing); // target - self
    }
}
