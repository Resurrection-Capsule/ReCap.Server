using ReCap.Server.Adapters.Scripting;
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
    internal LuaCoroutineScheduler Scheduler => _scheduler;

    public void Tick()
    {
        _clockSeconds += 0.05;
        _scheduler.Tick(_clockSeconds);
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
