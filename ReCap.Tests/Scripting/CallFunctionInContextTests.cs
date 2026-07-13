using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// nAbility/nModifier.CallFunctionInContext(instanceHandle, fn, ...args) (Ghidra @0x00a41c20, shared
// on both namespaces): run `fn` bound to the instance's context so in-fn getters (GetMyAgentID/
// GetPrivateTable) resolve to that instance, returning fn's results. Server-side we swap the caller
// thread's context to the resolved modifier instance for the protected call, then restore it.
public class CallFunctionInContextTests
{
    private static GameScriptContext MakeContext(out Game game)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "recap-cfic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(tempDir)));
        game = new Game(1, GameType.Matched, null);
        var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        return ctx;
    }

    [Fact]
    public void RunsFnBoundToInstance_Context_SharedPrivateTable_AndReturnValue()
    {
        using var ctx = MakeContext(out var game);
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            CtxAgent, CtxInitiator, CtxCreated, CtxRet = -1, -1, -1, -1
            function ProbeContext(x)
                CtxAgent = nModifier.GetMyAgentID()
                CtxInitiator = nModifier.GetMyInitiatorID()
                local pt = nThreadData.GetPrivateTable()
                CtxCreated = pt.created or -1   -- the flag [1] wrote proves this IS the instance's table
                pt.mark = 77
                return x * 2
            end
            nModifier.RegisterModifier("recap_cfic_probe", {
                [1] = function() nThreadData.GetPrivateTable().created = 1 end,
                [2] = function() nThread.WaitForever() end,
            })
            """), "probe");

        // [1] runs synchronously in CreateModifier, so the instance's shared private table exists.
        var id = ctx.CreateModifier(targetId: 10, casterId: 20, modifierGuid: ScriptVfs.Hash("recap_cfic_probe"), rank: 0);
        Assert.NotEqual(0u, id);

        ctx.Runtime.Execute(LuaFixtures.Compile($"CtxRet = nModifier.CallFunctionInContext({id}, ProbeContext, 21)"), "call");

        // agent = instance target, initiator = caster, table = instance's shared one, return passed through.
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile(
            "return CtxAgent == 10 and CtxInitiator == 20 and CtxCreated == 1 and CtxRet == 42")),
            "fn did not run bound to the instance context");
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
    }

    [Fact]
    public void UnknownInstanceHandle_IsHarmlessNoOp()
    {
        using var ctx = MakeContext(out var game);
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            Ran = 0
            function NeverRun() Ran = 1 end
            """), "probe");

        // No such modifier instance -> the function must not run, no error.
        ctx.Runtime.Execute(LuaFixtures.Compile("nModifier.CallFunctionInContext(999999, NeverRun)"), "call");

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return Ran == 0")));
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
    }
}
