using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// nGameObject.AddEffect/RemoveEffect/RemoveEffectIndex (Ghidra 2026-07-13): the object's 16-slot
// attached-effect array (obj+0x32) replicated via 0x9B ServerEvent (handler-slot 0x1c = wire 0x9B).
// AddEffect fills the first free slot (attached recipe), RemoveEffect clears the slot by effectId
// (stop recipe). Exercised through the real GameScriptContext so the slot bookkeeping is real.
public class EffectSlotTests
{
    private static GameScriptContext MakeContext(out Game game)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "recap-fx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(tempDir)));
        game = new Game(1, GameType.Matched, null);
        var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        return ctx;
    }

    [Fact]
    public void AddEffect_FillsFirstFreeSlotAndReturnsIndex()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);

        // Small effect ids round-trip through ResolveAssetHash unchanged, so the slot stores them exactly.
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nGameObject.AddEffect(10, 111) == 0")));
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nGameObject.AddEffect(10, 222) == 1")));

        var slots = game.Objects.Objects[10].EffectSlots;
        Assert.Equal(111u, slots[0]);
        Assert.Equal(222u, slots[1]);
    }

    [Fact]
    public void RemoveEffect_ClearsSlotMatchingEffectId()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);
        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.AddEffect(10, 111); nGameObject.AddEffect(10, 222)"), "add");

        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.RemoveEffect(10, 111)"), "rm");

        var slots = game.Objects.Objects[10].EffectSlots;
        Assert.Equal(0u, slots[0]);   // freed
        Assert.Equal(222u, slots[1]); // sibling untouched

        // The freed slot is reused by the next AddEffect.
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nGameObject.AddEffect(10, 333) == 0")));
        Assert.Equal(333u, slots[0]);
    }

    [Fact]
    public void RemoveEffectIndex_ClearsSpecificSlot()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);
        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.AddEffect(10, 111); nGameObject.AddEffect(10, 222)"), "add");

        ctx.Runtime.Execute(LuaFixtures.Compile("nGameObject.RemoveEffectIndex(10, 1)"), "rmi");

        var slots = game.Objects.Objects[10].EffectSlots;
        Assert.Equal(111u, slots[0]);
        Assert.Equal(0u, slots[1]);
    }

    [Fact]
    public void AddEffect_FullArrayReturnsMinusOne()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);
        var slots = game.Objects.Objects[10].EffectSlots;
        for (var i = 0; i < slots.Length; i++) slots[i] = (uint)(1000 + i); // all full

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nGameObject.AddEffect(10, 999) == -1")));
    }
}
