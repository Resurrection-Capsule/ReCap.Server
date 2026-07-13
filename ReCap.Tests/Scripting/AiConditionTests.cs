using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;

namespace ReCap.Tests.Scripting;

// EvaluateAiCondition: gambit conditions gate whether an enemy's ability fires. The distance gate
// (conditionProps {Distance, GreaterThan}) is the common melee/ranged range check.
public class AiConditionTests
{
    private static GameScriptContext MakeContext(out Game game)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "recap-aicond-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(tempDir)));
        game = new Game(1, GameType.Matched, null);
        var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        return ctx;
    }

    private static (string, string)[] Props(params (string, string)[] p) => p;

    [Fact]
    public void DistanceGate_LessThan_PassesWhenWithinRange()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);
        game.Objects.Spawn(20, 0, new Vector3(5, 0, 0), 1f, 1, true); // 5 away

        // distance(5) < 10 -> true
        Assert.True(ctx.EvaluateAiCondition("Condition_InRange", Props(("Distance", "10"), ("GreaterThan", "false")), 10, 20));
        // distance(5) < 3 -> false
        Assert.False(ctx.EvaluateAiCondition("Condition_InRange", Props(("Distance", "3"), ("GreaterThan", "false")), 10, 20));
    }

    [Fact]
    public void DistanceGate_GreaterThan_PassesWhenFar()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);
        game.Objects.Spawn(20, 0, new Vector3(8, 0, 0), 1f, 1, true); // 8 away

        Assert.True(ctx.EvaluateAiCondition("Condition_Far", Props(("Distance", "5"), ("GreaterThan", "true")), 10, 20));  // 8 > 5
        Assert.False(ctx.EvaluateAiCondition("Condition_Far", Props(("Distance", "10"), ("GreaterThan", "true")), 10, 20)); // 8 > 10 false
    }

    [Fact]
    public void UnknownCondition_OrMissingObjects_ReturnsFalse()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);
        // No props -> false; unmapped condition -> false; missing target -> false.
        Assert.False(ctx.EvaluateAiCondition("Condition_X", Props(), 10, 20));
        Assert.False(ctx.EvaluateAiCondition("Condition_LowHealth", Props(("HealthPercent", "50")), 10, 20));
        Assert.False(ctx.EvaluateAiCondition("Condition_InRange", Props(("Distance", "10"), ("GreaterThan", "false")), 10, 999));
    }
}
