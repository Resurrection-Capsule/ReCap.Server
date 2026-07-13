using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// Tier-2 tail: nAttribute.RemoveAttributeModifier@0x009fec10 (undo an AddAttributeModifier delta) and
// nThread.MoveTowardObject@0x00a03ad0 (blocking chase: aim at the target + yield until arrival).
public class TierTwoThreadAttributeTests
{
    private static GameScriptContext MakeContext(out Game game)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "recap-t2ta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(tempDir)));
        game = new Game(1, GameType.Matched, null);
        var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        return ctx;
    }

    [Fact]
    public void RemoveAttributeModifier_UndoesTheAddedDelta()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);
        game.Objects.Objects[10].Attributes[0] = 20f; // baseline Strength

        ctx.Runtime.Execute(LuaFixtures.Compile("ModH = nAttribute.AddAttributeModifier(10, 0, 5)"), "add");
        Assert.Equal(25f, game.Objects.Objects[10].Attributes[0]);

        ctx.Runtime.Execute(LuaFixtures.Compile("nAttribute.RemoveAttributeModifier(10, ModH)"), "rm");
        Assert.Equal(20f, game.Objects.Objects[10].Attributes[0]); // back to baseline

        // Removing the same handle again is a no-op (does not double-subtract).
        ctx.Runtime.Execute(LuaFixtures.Compile("nAttribute.RemoveAttributeModifier(10, ModH)"), "rm2");
        Assert.Equal(20f, game.Objects.Objects[10].Attributes[0]);
    }

    [Fact]
    public void MoveTowardObject_AimsGoalAtTargetAndParksUntilArrival()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);          // mover, default speed 5
        game.Objects.Spawn(20, 0, new Vector3(10, 0, 0), 1f, 1, true); // target, 10 away

        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nThread.CreateThreadForObject(10, function() nThread.MoveTowardObject(10, 20, 1) end)
            """), "chase");

        // The goal is aimed at the target and the coroutine parks (blocking until arrival).
        Assert.Equal(new Vector3(10, 0, 0), game.Objects.Objects[10].GoalPosition);
        Assert.True(ctx.Scheduler.HasThreadForObject(10));

        // estimate = (dist 10 - stop 1) / speed 5 = 1.8s; tick past it -> the chase coroutine resumes.
        for (var i = 0; i < 40; i++) ctx.Tick(); // 40 * 50ms = 2.0s
        Assert.False(ctx.Scheduler.HasThreadForObject(10));
    }
}
