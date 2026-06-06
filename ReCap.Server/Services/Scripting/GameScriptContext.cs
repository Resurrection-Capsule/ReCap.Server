using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Adapters.Scripting.Native;
using ReCap.Server.Domain.Gameplay;

namespace ReCap.Server.Services.Scripting;

public sealed class GameScriptContext : IScriptGameBridge, IDisposable
{
    private readonly Game _game;
    private readonly LuaRuntime _runtime;
    private readonly LuaCoroutineScheduler _scheduler;
    private readonly ScriptRegistry _registry;
    private double _clockSeconds;

    public GameScriptContext(Game game, ScriptEngine engine)
    {
        _game = game;
        _runtime = engine.CreateBootedRuntime($"game-{game.Id}");
        var state = ScriptContextRegistry.Get(_runtime.L)!;
        state.GameBridge = this;
        _scheduler = state.Scheduler!;
        _registry = state.Registry;
    }

    public ScriptRegistry Registry => _registry;
    internal LuaRuntime Runtime => _runtime;
    public LuaCoroutineScheduler Scheduler => _scheduler;

    public void Tick()
    {
        _clockSeconds += 0.05;
        _scheduler.Tick(_clockSeconds);
    }

    // Spawn a coroutine for the named ability's tick function.
    // Tick is called with (self, agentId, targetId, cursorX, cursorY, cursorZ, rank).
    // Context getters (nAbility.GetAgentID etc.) also resolve via per-thread invocation slot.
    // Retail contract (LUA_ABILITY_TICK_CONTRACT.md): 1-param ticks use getters; 9-param ticks
    // use positional args. Both work: Lua ignores extra caller args and getters always match.
    public bool InvokeAbility(uint abilityHash, uint agentId, uint targetId, float cursorX, float cursorY, float cursorZ, int rank)
    {
        var entry = _registry.Find(ScriptKind.Ability, abilityHash);
        if (entry is null || !entry.HasTick) return false;
        if (_scheduler.HasThreadForObject(agentId)) return false;

        var L = _runtime.L;
        var invocation = new AbilityInvocation(agentId, targetId, cursorX, cursorY, cursorZ, rank);

        LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, entry.TableRef);
        LuaNative.lua_getfield(L, -1, "tick");
        LuaNative.lua_insert(L, -2);

        // Push (self, agentId, targetId, cursorX, cursorY, cursorZ, rank) = 7 args + fn = 8 on stack
        LuaNative.lua_pushnumber(L, (float)agentId);
        LuaNative.lua_pushnumber(L, (float)targetId);
        LuaNative.lua_pushnumber(L, cursorX);
        LuaNative.lua_pushnumber(L, cursorY);
        LuaNative.lua_pushnumber(L, cursorZ);
        LuaNative.lua_pushnumber(L, (float)rank);

        // fn + self(table) + 6 number args = 8 total values on top
        var threadL = _scheduler.SpawnFromStack(L, agentId, 8,
            beforeFirstResume: threadState =>
            {
                var ctx = ScriptContextRegistry.Get(_runtime.L);
                ctx?.SetInvocation(threadState, invocation);
            });

        LuaNative.lua_settop(L, 0);
        return threadL != 0;
    }

    public void Dispose() => _runtime.Dispose();

    public bool TryGetPosition(uint objectId, out float x, out float y, out float z)
    {
        if (_game.TryGetObjectPosition(objectId, out var p)) { x = p.X; y = p.Y; z = p.Z; return true; }
        x = y = z = 0f;
        return false;
    }

    public float GetHitPoints(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.Health : 0f;

    public float GetMaxHitPoints(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.MaxHealth : 0f;

    public bool ObjectExists(uint objectId) => _game.Objects.Objects.ContainsKey(objectId);

    public byte GetTeam(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.Team : (byte)0;

    public uint GetTargetId(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.TargetId : 0u;
}
