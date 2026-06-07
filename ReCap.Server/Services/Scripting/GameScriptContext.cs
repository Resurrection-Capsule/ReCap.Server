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
    private uint _nextAbilityInstanceId;
    // lua_State is not thread-safe: Tick runs on the game loop, InvokeAbility on the RakNet
    // packet thread — serialize every entry into the runtime.
    private readonly Lock _luaGate = new();

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
        lock (_luaGate)
        {
            _clockSeconds += 0.05;
            _scheduler.Tick(_clockSeconds);
        }
    }

    // Spawn a coroutine for the named ability's tick function.
    // Tick is called with (self, agentId, targetId, cursorX, cursorY, cursorZ, rank).
    // Context getters (nAbility.GetAgentID etc.) also resolve via per-thread invocation slot.
    // Retail contract (LUA_ABILITY_TICK_CONTRACT.md): 1-param ticks use getters; 9-param ticks
    // use positional args. Both work: Lua ignores extra caller args and getters always match.
    public bool InvokeAbility(uint abilityHash, uint agentId, uint targetId, float cursorX, float cursorY, float cursorZ, int rank)
    {
        lock (_luaGate)
        {
            var entry = _registry.Find(ScriptKind.Ability, abilityHash);
            if (entry is null || !entry.HasTick) return false;
            if (_scheduler.HasThreadForObject(agentId)) return false;

            var L = _runtime.L;
            // TargetInRangeAtStart mirrors the client's cached at-cast flag (@0x00a410a0 reads a
            // byte stamped at ability start, not a live range test): true when the cast carried a
            // live target. Range-vs-distance refinement needs the ability's range prop (later).
            var targetInRange = targetId != 0 && _game.Objects.Objects.ContainsKey(targetId);
            var invocation = new AbilityInvocation(agentId, targetId, cursorX, cursorY, cursorZ, rank,
                AbilityHash: abilityHash,
                InstanceId: ++_nextAbilityInstanceId,
                TargetInRangeAtStart: targetInRange);

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

    public void SetTeam(uint objectId, byte team)
    {
        if (_game.Objects.Objects.TryGetValue(objectId, out var o)) o.Team = team;
    }

    public bool IsPlayerControlled(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) && o.PlayerControlled;

    public uint GetTargetId(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.TargetId : 0u;

    public bool TryGetAttributeValue(uint objectId, int attributeId, out float value)
    {
        value = 0f;
        return _game.Objects.Objects.TryGetValue(objectId, out var o)
            && o.Attributes.TryGetValue(attributeId, out value);
    }

    public IReadOnlyDictionary<int, float>? GetAttributeTable(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.Attributes : null;

    public bool TryGetOrientation(uint objectId, out float x, out float y, out float z, out float w)
    {
        if (_game.Objects.Objects.TryGetValue(objectId, out var o))
        {
            x = o.Orientation.X; y = o.Orientation.Y; z = o.Orientation.Z; w = o.Orientation.W;
            return true;
        }
        x = y = z = 0f; w = 1f;
        return false;
    }

    public void BroadcastAnimationState(uint objectId, uint stateHash) =>
        _game.BroadcastAnimationState(objectId, stateHash);

    // Client wrapper @0x00a0aed0: QueryObjectsInRadius capped at 256, no alive gate; the table
    // filter (nSporeLabs.damageableObjectTypes whitelist) is approximated server-side as
    // "has a combatant" (MaxHealth > 0) until per-noun type ids are parsed.
    public IReadOnlyList<uint> QueryObjectsInRadius(float x, float y, float z, float radius, bool damageableOnly)
    {
        var center = new System.Numerics.Vector3(x, y, z);
        var radiusSq = radius * radius;
        var result = new List<uint>();
        foreach (var (id, obj) in _game.Objects.Objects)
        {
            if (damageableOnly && obj.MaxHealth <= 0f) continue;
            if (System.Numerics.Vector3.DistanceSquared(center, obj.Position) > radiusSq) continue;
            result.Add(id);
            if (result.Count >= 256) break;
        }
        return result;
    }

    public float ApplyHeal(uint targetId, float amount)
    {
        if (!_game.Objects.Objects.TryGetValue(targetId, out var obj) || obj.MaxHealth <= 0f) return 0f;
        var before = obj.Health;
        obj.Health = Math.Clamp(obj.Health + amount, 0f, obj.MaxHealth);
        ReCap.Server.Util.Logging.Log.Game.Info(
            $"[lua] HealDamage target={targetId} amount={amount:F1} hp {before:F1}→{obj.Health:F1}");
        return obj.Health - before;
    }

    public void MarkForDelete(uint objectId) => _game.Objects.Remove(objectId);

    public void SetVisible(uint objectId, bool visible)
    {
        // GameObject visibility flag lands with the object-stream phase; accept silently for now.
    }
}
