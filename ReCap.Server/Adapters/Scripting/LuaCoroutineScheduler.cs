using ReCap.Server.Adapters.Scripting.Api;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting;

public sealed class LuaCoroutineScheduler(nint mainState)
{
    private sealed class ThreadEntry
    {
        public required nint ThreadL { get; init; }
        public required int ThreadRef { get; init; }
        public uint ObjectId { get; set; }
        public bool Sleeping { get; set; }
        public double? WakeAtSeconds { get; set; }
        public Func<bool>? WakeWhen { get; set; }
        // Optional: pushes return values onto the thread stack when it wakes and returns their count,
        // so a wait can hand results back to the yielding Lua (e.g. WaitForProjectile's impact result).
        public Func<nint, int>? ResumeValues { get; set; }
        // Fired when the coroutine finishes normally (LUA_OK), before teardown, with its thread L on
        // the stack — lets a pcall-style handler ([4] event) read its return value. Not fired on error
        // or on StopThread.
        public Action<nint>? OnComplete { get; set; }
    }

    private readonly Dictionary<nint, ThreadEntry> _threads = [];
    private readonly Dictionary<uint, nint> _byObject = [];
    private double _now;

    public double Now => _now;
    public int ActiveCount => _threads.Count;
    public int ErrorCount { get; private set; }

    public bool HasThreadForObject(uint objectId) => _byObject.ContainsKey(objectId);

    public nint Spawn(nint callerL, uint objectId, int fnIndex, int argCount)
    {
        if (objectId != 0 && _byObject.ContainsKey(objectId)) return 0;
        var threadL = LuaNative.lua_newthread(mainState);
        var threadRef = LuaNative.luaL_ref(mainState, LuaNative.LUA_REGISTRYINDEX);
        LuaNative.lua_pushvalue(callerL, fnIndex);
        LuaNative.lua_xmove(callerL, threadL, 1);
        for (var i = 0; i < argCount; i++)
        {
            LuaNative.lua_pushvalue(callerL, fnIndex + 1 + i);
            LuaNative.lua_xmove(callerL, threadL, 1);
        }
        RegisterAndResume(threadL, threadRef, objectId, argCount, beforeFirstResume: null);
        return threadL;
    }

    // Moves valuesOnTop stack values (fn first, then args) from callerL to a new thread.
    // beforeFirstResume is invoked after thread registration but before the first resume,
    // allowing the caller to set per-thread state (e.g. SetInvocation) before Lua runs.
    public nint SpawnFromStack(nint callerL, uint objectId, int valuesOnTop, Action<nint>? beforeFirstResume = null,
        Action<nint>? onComplete = null)
    {
        if (objectId != 0 && _byObject.ContainsKey(objectId)) return 0;
        var threadL = LuaNative.lua_newthread(mainState);
        var threadRef = LuaNative.luaL_ref(mainState, LuaNative.LUA_REGISTRYINDEX);
        var argCount = valuesOnTop - 1;
        LuaNative.lua_xmove(callerL, threadL, valuesOnTop);
        RegisterAndResume(threadL, threadRef, objectId, argCount, beforeFirstResume, onComplete);
        return threadL;
    }

    private void RegisterAndResume(nint threadL, int threadRef, uint objectId, int argCount, Action<nint>? beforeFirstResume,
        Action<nint>? onComplete = null)
    {
        var entry = new ThreadEntry { ThreadL = threadL, ThreadRef = threadRef, ObjectId = objectId, OnComplete = onComplete };
        _threads[threadL] = entry;
        if (objectId != 0) _byObject[objectId] = threadL;
        var context = ScriptContextRegistry.Get(mainState);
        if (context is not null) ScriptContextRegistry.Register(threadL, context);
        var tag = StubTelemetry.GetTag(mainState);
        StubTelemetry.TagState(threadL, tag);
        LuaRuntime.InstallWatchdog(threadL);
        beforeFirstResume?.Invoke(threadL);
        Resume(entry, argCount);
    }

    public void RegisterYield(nint threadL, bool sleeping, double? wakeAt, Func<bool>? wakeWhen = null,
        Func<nint, int>? resumeValues = null)
    {
        if (!_threads.TryGetValue(threadL, out var entry)) return;
        entry.Sleeping = sleeping;
        entry.WakeAtSeconds = wakeAt;
        entry.WakeWhen = wakeWhen;
        entry.ResumeValues = resumeValues;
    }

    public void WakeObject(uint objectId)
    {
        if (_byObject.TryGetValue(objectId, out var threadL) && _threads.TryGetValue(threadL, out var entry))
            entry.Sleeping = false;
    }

    // Force-terminate a specific coroutine by its thread handle (used to stop a per-instance modifier
    // tick when the modifier is deleted). Object-keyed threads use HasThreadForObject; detached
    // threads (objectId 0, e.g. modifier ticks) can only be stopped by their thread handle.
    public bool StopThread(nint threadL)
    {
        if (!_threads.TryGetValue(threadL, out var entry)) return false;
        Release(entry);
        return true;
    }

    public bool HasThread(nint threadL) => _threads.ContainsKey(threadL);

    public void Tick(double nowSeconds)
    {
        _now = nowSeconds;
        foreach (var entry in _threads.Values.ToList())
        {
            if (entry.Sleeping) continue;
            if (entry.WakeWhen is not null)
            {
                var predicateReady = SafeEvaluate(entry.WakeWhen);
                var timeoutReady = entry.WakeAtSeconds is double w && nowSeconds >= w;
                if (!predicateReady && !timeoutReady) continue;
            }
            else if (entry.WakeAtSeconds is double wake && nowSeconds < wake) continue;
            entry.WakeAtSeconds = null;
            entry.WakeWhen = null;
            var resumeArgs = 0;
            if (entry.ResumeValues is { } push)
            {
                resumeArgs = SafeResumeValues(push, entry.ThreadL);
                entry.ResumeValues = null;
            }
            Resume(entry, resumeArgs);
        }
    }

    private static bool SafeEvaluate(Func<bool> predicate)
    {
        try { return predicate(); } catch { return false; }
    }

    private static void SafeComplete(Action<nint> onComplete, nint threadL)
    {
        try { onComplete(threadL); } catch { }
    }

    private static int SafeResumeValues(Func<nint, int> push, nint threadL)
    {
        try { return push(threadL); } catch { return 0; }
    }

    private void Resume(ThreadEntry entry, int argCount)
    {
        var status = LuaNative.lua_resume(entry.ThreadL, argCount);
        if (status == LuaNative.LUA_YIELD) return;
        if (status == LuaNative.LUA_OK)
        {
            if (entry.OnComplete is { } onComplete) SafeComplete(onComplete, entry.ThreadL);
        }
        else
        {
            var message = LuaNative.ToManagedString(entry.ThreadL, -1) ?? "unknown";
            Util.Logging.Log.Lua.Error($"[coroutine] object {entry.ObjectId}: {message}");
            ErrorCount++;
        }
        Release(entry);
    }

    private void Release(ThreadEntry entry)
    {
        // Idempotent: a modifier tick that removes itself (nModifier.MarkForDelete → RemoveModifier →
        // StopThread) Releases its own thread mid-resume; when that resume later returns, Resume calls
        // Release again. Guard so the registry ref is unref'd exactly once (a double luaL_unref corrupts
        // the ref free list). Expiry is script-driven this way — see modifier DoT [2] self-removal.
        if (!_threads.Remove(entry.ThreadL)) return;
        if (entry.ObjectId != 0 && _byObject.TryGetValue(entry.ObjectId, out var l) && l == entry.ThreadL)
            _byObject.Remove(entry.ObjectId);
        var context = ScriptContextRegistry.Get(entry.ThreadL);
        context?.RemoveInvocation(entry.ThreadL);
        if (context is not null)
        {
            if (context.TryTakePrivateTableRef(entry.ThreadL, out var privateRef))
                LuaNative.luaL_unref(mainState, LuaNative.LUA_REGISTRYINDEX, privateRef);
            // Instance-owned shared table (modifier +0x170): unbind this thread only; the owning
            // modifier instance frees the table on removal.
            context.UnbindSharedPrivateTable(entry.ThreadL);
            context.RemoveThreadData(entry.ThreadL);
        }
        ScriptContextRegistry.Unregister(entry.ThreadL);
        StubTelemetry.UntagState(entry.ThreadL);
        LuaNative.luaL_unref(mainState, LuaNative.LUA_REGISTRYINDEX, entry.ThreadRef);
    }
}
