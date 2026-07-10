using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// A modifier's index [1] activate, [2] tick and [3] deactivate share ONE private table (retail
// instance+0x170), so state written in activate is visible to tick — verified 2026-07-10 against
// nModifier_SpawnTickCoroutine @0x009e2940 (thread.data = instance+0x170) + RunActivate @0x009e3410
// (runs on the same instance+0x2c thread). Uses a synthetic modifier so the assertion is exact.
public class ModifierSharedContextTests
{
    private static GameScriptContext MakeContext(out Game game)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "recap-modctx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(tempDir)));
        game = new Game(1, GameType.Matched, null);
        var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        return ctx;
    }

    [Fact]
    public void ActivateWritesPrivateTable_TickReadsSameTable()
    {
        using var ctx = MakeContext(out var game);

        // [2] tick yields first (so [1] activate runs during the wait, as in retail), then reads the
        // flag [1] wrote into the shared private table. If tick got its own table the flag stays nil.
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            SharedProbe = -1
            nModifier.RegisterModifier("recap_shared_ctx_probe", {
                [1] = function() nThreadData.GetPrivateTable().flag = 123 end,
                [2] = function()
                        nThread.WaitForXSeconds(0.1)
                        SharedProbe = nThreadData.GetPrivateTable().flag or -2
                        nThread.WaitForever()
                      end,
                [3] = function() end,
            })
            """), "probe");

        var hash = ScriptVfs.Hash("recap_shared_ctx_probe");
        var id = ctx.CreateModifier(targetId: 10, casterId: 20, modifierGuid: hash, rank: 0);
        Assert.NotEqual(0u, id);

        // Before the tick's wait elapses, activate has run but tick hasn't read yet.
        for (var i = 0; i < 5; i++) ctx.Tick(); // 5 * 50ms = 250ms > 100ms wait

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return SharedProbe == 123")),
            "tick did not read the value activate wrote into the shared private table");

        Assert.True(ctx.RemoveModifier(id));
        Assert.Null(game.Modifiers.Get(id));
    }

    [Fact]
    public void TickCanSelfMarkForDelete_ScriptDrivenExpiry_NoDoubleRelease()
    {
        using var ctx = MakeContext(out var game);
        // Retail modifier expiry is script-driven: the [2] tick removes its own instance via
        // nModifier.MarkForDelete (there is no C++ duration sweep). That StopThreads the running tick
        // from inside its own resume — the scheduler must survive the re-entrant release.
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            SelfId = 0
            nModifier.RegisterModifier("recap_self_expire", {
                [2] = function()
                        nThread.WaitForXSeconds(0.1)
                        nModifier.MarkForDelete(SelfId)
                      end,
            })
            """), "probe");
        var hash = ScriptVfs.Hash("recap_self_expire");

        var id = ctx.CreateModifier(10, 20, hash, 0);
        Assert.NotEqual(0u, id);
        ctx.Runtime.Execute(LuaFixtures.Compile($"SelfId = {id}"), "id");

        for (var i = 0; i < 5; i++) ctx.Tick(); // cross the 0.1s tick; [2] self-removes

        Assert.Null(game.Modifiers.Get(id));       // gone, replicated via 0xA4
        Assert.Equal(0, ctx.Scheduler.ErrorCount); // no crash / no corruption from the self-release

        // The registry ref free list is intact — a fresh modifier still creates and removes cleanly.
        var id2 = ctx.CreateModifier(10, 20, hash, 0);
        Assert.NotEqual(0u, id2);
        Assert.True(ctx.RemoveModifier(id2));
    }

    [Fact]
    public void StacksReapply_FiresIndex4StackEvent_BumpsStackCount()
    {
        using var ctx = MakeContext(out var game);
        // activationType Stacks(4): re-requesting on the same caster reuses the instance and fires its
        // [4] StackModifier(32) event, whose Lua bumps the stack + resets duration (retail FirebombDot).
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nModifier.RegisterModifier("recap_stacks_probe", {
                activationType = 4,
                [2] = function() nThread.WaitForever() end,
                [4] = function(ev)
                        if nAbility.GetAbilityEventType(ev) == 32 then
                            nModifier.IncrementStackCount()
                            nModifier.ResetDuration()
                        end
                        return true
                      end,
            })
            """), "probe");
        var hash = ScriptVfs.Hash("recap_stacks_probe");

        var id1 = ctx.CreateModifier(10, 20, hash, 0);
        Assert.Equal(1, game.Modifiers.Get(id1)!.StackCount);

        var id2 = ctx.CreateModifier(10, 20, hash, 0); // same caster → reuse + [4] bump
        Assert.Equal(id1, id2);
        Assert.Equal(2, game.Modifiers.Get(id1)!.StackCount);

        var id3 = ctx.CreateModifier(10, 20, hash, 0);
        Assert.Equal(id1, id3);
        Assert.Equal(3, game.Modifiers.Get(id1)!.StackCount);
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
    }

    [Fact]
    public void StacksReapply_Index4ReturnsFalse_RemovesInstance()
    {
        using var ctx = MakeContext(out var game);
        // Retail RunReapplyEvent removes the instance when the [4] handler returns false.
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nModifier.RegisterModifier("recap_reject_probe", {
                activationType = 4,
                [2] = function() nThread.WaitForever() end,
                [4] = function(ev) return false end,
            })
            """), "probe");
        var hash = ScriptVfs.Hash("recap_reject_probe");

        var id1 = ctx.CreateModifier(10, 20, hash, 0);
        Assert.NotNull(game.Modifiers.Get(id1));

        var id2 = ctx.CreateModifier(10, 20, hash, 0); // reuse → [4] returns false → existing removed
        Assert.Equal(id1, id2);
        Assert.Null(game.Modifiers.Get(id1));
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
    }

    [Fact]
    public void TookDamageEvent_FiresIndex4OnSubscribedModifier_WithTypedPayload()
    {
        using var ctx = MakeContext(out var game);
        // A modifier subscribed to TookDamage(1) gets its [4] fired when its target takes damage, with
        // the typed payload the real handlers read (ThornsPassive/QuantumStateBuff).
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            EvType, EvAtk, EvAmt, EvDesc, EvMelee = -1, -1, -1, -1, false
            nModifier.RegisterModifier("recap_tookdmg_probe", {
                handledEvents = 1,
                [2] = function() nThread.WaitForever() end,
                [4] = function(ev)
                        EvType = nAbility.GetAbilityEventType(ev)
                        if EvType == 1 then
                            EvAtk = nAbility.GetAbilityEventGUIDData(ev, 1)
                            EvAmt = nAbility.GetAbilityEventFloatData(ev, 2)
                            EvDesc = nAbility.GetAbilityEventIntData(ev, 3)
                            EvMelee = nAbility.CheckDescriptors_AnyMatch(8, EvDesc)
                        end
                        return true
                      end,
            })
            NoEvent = 0
            nModifier.RegisterModifier("recap_noevent_probe", {
                handledEvents = 0,
                [4] = function(ev) NoEvent = NoEvent + 1 return true end,
            })
            """), "probe");

        ctx.CreateModifier(10, 20, ScriptVfs.Hash("recap_tookdmg_probe"), 0);
        ctx.CreateModifier(10, 20, ScriptVfs.Hash("recap_noevent_probe"), 0);

        ctx.DispatchTookDamage(targetId: 10, attackerId: 99, amount: 42f, descriptors: 8);

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile(
            "return EvType == 1 and EvAtk == 99 and EvAmt == 42 and EvDesc == 8 and EvMelee == true")));
        // The unsubscribed modifier's [4] must NOT fire.
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return NoEvent == 0")));
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
    }

    [Fact]
    public void PriorityDispel_AgentModifierDispelsLowerInterruptible_KeepsNonInterruptibleAndHigher()
    {
        using var ctx = MakeContext(out var game);
        // requiresAgent gates the pre-pass; a new modifier dispels existing ones flagged
        // deactivateOnInterrupt whose modifierPriority is <= the newcomer's (retail dispel loop).
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            local function persist() return function() nThread.WaitForever() end end
            nModifier.RegisterModifier("recap_slow", { requiresAgent=true, modifierPriority=450, deactivateOnInterrupt=true, [2]=persist() })
            nModifier.RegisterModifier("recap_root", { requiresAgent=true, modifierPriority=500, deactivateOnInterrupt=true, [2]=persist() })
            nModifier.RegisterModifier("recap_poison", { requiresAgent=true, modifierPriority=100, deactivateOnInterrupt=false, [2]=persist() })
            """), "probe");

        var slowId = ctx.CreateModifier(10, 20, ScriptVfs.Hash("recap_slow"), 0);
        var poisonId = ctx.CreateModifier(10, 20, ScriptVfs.Hash("recap_poison"), 0);
        // Applying the higher-priority Root dispels the interruptible Slow but not the poison DoT.
        var rootId = ctx.CreateModifier(10, 20, ScriptVfs.Hash("recap_root"), 0);

        Assert.Null(game.Modifiers.Get(slowId));      // dispelled (450 <= 500, deactivateOnInterrupt)
        Assert.NotNull(game.Modifiers.Get(poisonId)); // survives — not deactivateOnInterrupt
        Assert.NotNull(game.Modifiers.Get(rootId));
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
    }

    [Fact]
    public void PriorityDispel_LowerPriorityNewcomer_DoesNotDispelHigher()
    {
        using var ctx = MakeContext(out var game);
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            local function persist() return function() nThread.WaitForever() end end
            nModifier.RegisterModifier("recap_root2", { requiresAgent=true, modifierPriority=500, deactivateOnInterrupt=true, [2]=persist() })
            nModifier.RegisterModifier("recap_slow2", { requiresAgent=true, modifierPriority=450, deactivateOnInterrupt=true, [2]=persist() })
            """), "probe");

        var rootId = ctx.CreateModifier(10, 20, ScriptVfs.Hash("recap_root2"), 0);
        ctx.CreateModifier(10, 20, ScriptVfs.Hash("recap_slow2"), 0); // weaker — must not dispel the root

        Assert.NotNull(game.Modifiers.Get(rootId)); // higher priority outranks the newcomer
    }

    [Fact]
    public void DealtDamageEvent_FiresIndex4OnAttackersModifier_WithVictimInSlot1()
    {
        using var ctx = MakeContext(out var game);
        // DealtDamage(8192) fires on the ATTACKER's subscribed modifiers; the handler reads GUID[1] =
        // the victim it hit (QuantumStateBuff on-hit proc).
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            DealtType, DealtVictim, WrongFire = -1, -1, 0
            nModifier.RegisterModifier("recap_dealt_probe", {
                handledEvents = 8192,
                [2] = function() nThread.WaitForever() end,
                [4] = function(ev)
                        DealtType = nAbility.GetAbilityEventType(ev)
                        if DealtType == 8192 then DealtVictim = nAbility.GetAbilityEventGUIDData(ev, 1) end
                        return true
                      end,
            })
            nModifier.RegisterModifier("recap_tookonly_probe", {
                handledEvents = 1,
                [4] = function(ev) WrongFire = WrongFire + 1 return true end,
            })
            """), "probe");

        ctx.CreateModifier(50, 20, ScriptVfs.Hash("recap_dealt_probe"), 0);
        ctx.CreateModifier(50, 20, ScriptVfs.Hash("recap_tookonly_probe"), 0);

        ctx.DispatchDealtDamage(attackerId: 50, targetId: 77);

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return DealtType == 8192 and DealtVictim == 77")));
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return WrongFire == 0"))); // TookDamage-only must not fire
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
    }

    [Fact]
    public void RemovedModifierFreesPrivateTable_NoLeakOnReuse()
    {
        using var ctx = MakeContext(out var game);
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nModifier.RegisterModifier("recap_free_probe", {
                [2] = function() nThread.WaitForever() end,
            })
            """), "probe");
        var hash = ScriptVfs.Hash("recap_free_probe");

        // Create + remove many times; a mismanaged instance table would double-free or leak refs.
        for (var i = 0; i < 20; i++)
        {
            var id = ctx.CreateModifier(10, 20, hash, 0);
            Assert.NotEqual(0u, id);
            ctx.Tick();
            Assert.True(ctx.RemoveModifier(id));
        }
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
    }
}
