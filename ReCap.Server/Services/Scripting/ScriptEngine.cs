using ReCap.Server.Adapters.Scripting;

namespace ReCap.Server.Services.Scripting;

public sealed class ScriptEngine(ScriptVfs vfs)
{
    private static readonly uint[] BootGroupOrder =
    [
        0x3681D755, 0xDA09176B, 0xFC0FF8F5, 0xD2FCB262, 0x7153BBB1,
        0xD79FA88C, 0xC130A42A, 0xB2A79C5C, 0xEE84D09A, 0x24F78AA1,
    ];

    public BootReport ExecuteBootScripts(LuaRuntime runtime)
    {
        var report = new BootReport();
        foreach (var group in BootGroupOrder)
        {
            foreach (var (instance, bytes) in vfs.GetGroup(group))
            {
                report.Total++;
                try
                {
                    runtime.Execute(bytes, $"0x{group:X8}!0x{instance:X8}");
                }
                catch (LuaScriptException ex)
                {
                    report.Failures.Add(ex.Message);
                    Util.Logging.Log.Lua.Error(ex.Message);
                }
            }
        }
        return report;
    }

    public LuaRuntime CreateBootedRuntime()
    {
        var runtime = LuaRuntime.CreateSandboxedState(name => vfs.GetChunk(ScriptVfs.ParseReference(name)));
        ExecuteBootScripts(runtime);
        return runtime;
    }
}

public sealed class BootReport
{
    public int Total { get; set; }
    public List<string> Failures { get; } = [];
}
