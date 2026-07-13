using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// Tier-3 slice (Ghidra 2026-07-13): nMathUtil.DistanceToLine/RotateVectorByAxisAngle (pure math),
// nDebug.LogToConsole/Assert (script diagnostics), nGameSimulator.IsChainGame, nGameObject.KillObject.
public class TierThreeNativesTests
{
    private static GameScriptContext MakeContext(out Game game)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "recap-t3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(tempDir)));
        game = new Game(1, GameType.Matched, null);
        var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        return ctx;
    }

    [Fact]
    public void DistanceToLine_IsPointToSegmentDistance()
    {
        using var ctx = MakeContext(out _);
        // P=(0,3,0), segment A=(-1,0,0)->B=(1,0,0): closest is (0,0,0), distance 3.
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile(
            "return nMathUtil.DistanceToLine(0,3,0, -1,0,0, 1,0,0) == 3")));
        // P=(5,0,0) projects past B=(1,0,0): clamps to B, distance 4.
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile(
            "return nMathUtil.DistanceToLine(5,0,0, -1,0,0, 1,0,0) == 4")));
    }

    [Fact]
    public void RotateVectorByAxisAngle_RotatesAboutAxisInRadians()
    {
        using var ctx = MakeContext(out _);
        // Rotate (1,0,0) by pi/2 about +Y -> (0,0,-1) in a right-handed frame.
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("""
            local x, y, z = nMathUtil.RotateVectorByAxisAngle(1,0,0, 0,1,0, math.pi/2)
            return math.abs(x) < 0.001 and math.abs(y) < 0.001 and math.abs(z + 1) < 0.001
            """)));
    }

    [Fact]
    public void Debug_LogAndAssert_DoNotThrow()
    {
        using var ctx = MakeContext(out _);
        // Diagnostics only — must run without error and return nothing.
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nDebug.LogToConsole("hello from a test")
            nDebug.LogToConsole(42)
            nDebug.Assert(1)
            nDebug.Assert(nil, "should warn but not crash")
            nDebug.DrawCircle(0,0,0, 1,0,0, 5)
            """), "dbg");
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return true")));
    }

    [Fact]
    public void IsChainGame_TrueForTheChainDungeon()
    {
        using var ctx = MakeContext(out _);
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nGameSimulator.IsChainGame() == true")));
    }

    [Fact]
    public void KillObject_RunsDeathPathAndDespawns()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);
        Assert.True(game.Objects.Objects.ContainsKey(10));

        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.KillObject(10)"), "kill");

        Assert.False(game.Objects.Objects.ContainsKey(10)); // OnObjectDeath despawned it
    }
}
