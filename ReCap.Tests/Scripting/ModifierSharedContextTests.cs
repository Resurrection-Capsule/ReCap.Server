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
