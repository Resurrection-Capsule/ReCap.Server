using ReCap.Server.Util.Logging;

namespace ReCap.Server.Domain.Gameplay;

// Handle-based, cancellable delayed/repeating task primitive for plain C# game code — the
// non-Lua sibling of LuaCoroutineScheduler. Mirrors C++ Core::Async::Scheduler +
// Instance::AddTask/CancelTask, but tick-driven off Game.Update's 50ms heartbeat instead of a
// background thread (no extra locking: Game.Update is already serialized per game).
public sealed class GameScheduler
{
    private sealed class ScheduledTask
    {
        public required uint Id;
        public required Action<uint> Callback;
        public required double NextRunSeconds;
        public double? RepeatIntervalSeconds; // null = one-shot
        public bool Cancelled;
    }

    private readonly Dictionary<uint, ScheduledTask> _tasks = new();
    private readonly List<ScheduledTask> _due = new();
    private uint _nextId;
    private double _now;

    // Run callback after delaySeconds; if repeatIntervalSeconds is set, keep re-running at that
    // interval until CancelTask/CancelAll. Returns a handle for cancellation.
    public uint AddTask(double delaySeconds, Action<uint> callback, double? repeatIntervalSeconds = null)
    {
        do { _nextId++; } while (_nextId == 0 || _tasks.ContainsKey(_nextId));
        var id = _nextId;
        _tasks[id] = new ScheduledTask
        {
            Id = id,
            Callback = callback,
            NextRunSeconds = _now + Math.Max(0, delaySeconds),
            RepeatIntervalSeconds = repeatIntervalSeconds
        };
        return id;
    }

    // Soft-cancel: safe to call from inside a callback; the entry is dropped on the next Tick.
    public void CancelTask(uint id)
    {
        if (_tasks.TryGetValue(id, out var task)) task.Cancelled = true;
    }

    public void CancelAll() => _tasks.Clear();

    // Called once per Game.Update with the same wall-clock delta already computed there.
    public void Tick(double deltaSeconds)
    {
        _now += deltaSeconds;

        _due.Clear();
        foreach (var task in _tasks.Values)
        {
            if (task.Cancelled || task.NextRunSeconds > _now) continue;
            _due.Add(task);
        }
        if (_due.Count == 0) return;

        foreach (var task in _due)
        {
            if (task.Cancelled) { _tasks.Remove(task.Id); continue; }

            try { task.Callback(task.Id); }
            catch (Exception ex) { Log.Game.Error($"GameScheduler task {task.Id} failed: {ex}"); }

            if (task.RepeatIntervalSeconds is double interval && !task.Cancelled)
                task.NextRunSeconds = _now + interval;
            else
                _tasks.Remove(task.Id);
        }
    }
}
