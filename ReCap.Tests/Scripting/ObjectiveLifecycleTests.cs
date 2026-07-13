using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// Objective lifecycle foundation: ActivateObjectives runs each objective's Init ([1]) with its
// objective-id context bound, so SetObjectiveIntData keys per objective; firing the Death event ([2],
// nObjectiveEvents.Death=4) runs the handler which reads the payload and updates progress.
public class ObjectiveLifecycleTests
{
    private static GameScriptContext MakeContext(out Game game)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "recap-objlife-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(tempDir)));
        game = new Game(1, GameType.Matched, null);
        var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        return ctx;
    }

    [Fact]
    public void ActivateObjectives_RunsInit_KeyedPerObjective()
    {
        using var ctx = MakeContext(out _);
        // Two objectives set the SAME (target,index) slot to different values in their Init; keying by
        // objective id keeps them distinct.
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nObjective.RegisterObjective("recap_obj_a", {
                handledEvents = 0,
                [1] = function() nObjective.SetObjectiveIntData(255, 0, 10) end,
            })
            nObjective.RegisterObjective("recap_obj_b", {
                handledEvents = 0,
                [1] = function() nObjective.SetObjectiveIntData(255, 0, 20) end,
            })
            """), "reg");

        ctx.ActivateObjectives(new[] { "recap_obj_a", "recap_obj_b" });

        Assert.Equal(10, ctx.PeekObjectiveInt(ScriptVfs.Hash("recap_obj_a"), 255, 0));
        Assert.Equal(20, ctx.PeekObjectiveInt(ScriptVfs.Hash("recap_obj_b"), 255, 0));
    }

    [Fact]
    public void DeathEvent_RunsHandleEventOnActiveObjective_UpdatesProgress()
    {
        using var ctx = MakeContext(out var game);
        // A kill-based objective records the killed object id (event GUID slot 1) into its progress.
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nObjective.RegisterObjective("recap_obj_kills", {
                handledEvents = 4,  -- nObjectiveEvents.Death
                [1] = function() nObjective.SetObjectiveIntData(255, 0, 0) end,
                [2] = function(evType, evHandle)
                        local victim = nObjective.GetObjectiveEventGUIDData(evHandle, 1)
                        nObjective.SetObjectiveIntData(255, 0, victim)
                      end,
            })
            """), "reg");
        ctx.ActivateObjectives(new[] { "recap_obj_kills" });
        var id = ScriptVfs.Hash("recap_obj_kills");
        Assert.Equal(0, ctx.PeekObjectiveInt(id, 255, 0)); // Init set 0

        // Spawn + kill a combatant enemy -> OnObjectDeath fires the Death objective event with the victim.
        game.Objects.Spawn(77, 0, Vector3.Zero, 1f, 2, false);
        game.Objects.Objects[77].MaxHealth = 10f;
        game.OnObjectDeath(77);

        Assert.Equal(77, ctx.PeekObjectiveInt(id, 255, 0)); // handler recorded the killed object
    }

    [Fact]
    public void ObjectiveInit_UsingCreatePrivateTable_RunsWithoutError()
    {
        using var ctx = MakeContext(out _);
        // Real objectives (DefeatAllMonsters) do `local pt = nThreadData.CreatePrivateTable(); pt.x = ...`
        // in Init. CreatePrivateTable was unregistered → returned nil → "index a nil value" killed Init.
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nObjective.RegisterObjective("recap_obj_pt", {
                handledEvents = 0,
                [1] = function()
                        local pt = nThreadData.CreatePrivateTable()
                        pt.reportPercentage = 25
                        nObjective.SetObjectiveIntData(255, 0, pt.reportPercentage)
                      end,
            })
            """), "reg");
        ctx.ActivateObjectives(new[] { "recap_obj_pt" });

        Assert.Equal(0, ctx.Scheduler.ErrorCount); // Init ran clean (no nil-index)
        Assert.Equal(25, ctx.PeekObjectiveInt(ScriptVfs.Hash("recap_obj_pt"), 255, 0));
    }

    [Fact]
    public void InactiveObjective_DoesNotReceiveEvents()
    {
        using var ctx = MakeContext(out var game);
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            Fired = 0
            nObjective.RegisterObjective("recap_obj_inactive", {
                handledEvents = 4,
                [2] = function() Fired = Fired + 1 end,
            })
            """), "reg");
        // NOT activated. A death event must not run its handler.
        game.Objects.Spawn(77, 0, Vector3.Zero, 1f, 2, false);
        game.Objects.Objects[77].MaxHealth = 10f;
        game.OnObjectDeath(77);

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return Fired == 0")));
    }
}
