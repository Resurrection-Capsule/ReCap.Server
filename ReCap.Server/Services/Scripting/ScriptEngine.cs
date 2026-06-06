using ReCap.Server.Adapters.Scripting;

namespace ReCap.Server.Services.Scripting;

public sealed class ScriptEngine(ScriptVfs vfs)
{
    private static readonly uint[] BootGroupOrder =
    [
        0x3681D755, 0xDA09176B, 0xFC0FF8F5, 0xD2FCB262, 0x7153BBB1,
        0xD79FA88C, 0xC130A42A, 0xB2A79C5C, 0xEE84D09A, 0x24F78AA1,
    ];

    // global.lua defines the Class OOP helper required by position-13 chunks (C6)
    private static readonly (uint Group, uint Instance)[] PreBootChunks =
    [
        (0x3681D755, 0x57572DAC),
    ];

    public BootReport ExecuteBootScripts(LuaRuntime runtime)
    {
        var report = new BootReport();
        var executed = new HashSet<(uint, uint)>();

        foreach (var (g, i) in PreBootChunks)
        {
            var bytes = vfs.GetChunk(new ScriptKey(g, i, 0));
            if (bytes is null) continue;
            report.Total++;
            executed.Add((g, i));
            try
            {
                runtime.Execute(bytes, $"0x{g:X8}!0x{i:X8}");
            }
            catch (LuaScriptException ex)
            {
                ClassifyFailure(report, ex.Message);
            }
        }

        foreach (var group in BootGroupOrder)
        {
            foreach (var (instance, bytes) in vfs.GetGroup(group))
            {
                if (!executed.Add((group, instance))) continue;
                report.Total++;
                try
                {
                    runtime.Execute(bytes, $"0x{group:X8}!0x{instance:X8}");
                }
                catch (LuaScriptException ex)
                {
                    ClassifyFailure(report, ex.Message);
                }
            }
        }
        return report;
    }

    private static void ClassifyFailure(BootReport report, string message)
    {
        if (message.Contains("require: chunk not found"))
        {
            report.MissingRequires.Add(message);
            Util.Logging.Log.Lua.Warn($"[retail-missing] {message}");
        }
        else
        {
            report.Failures.Add(message);
            Util.Logging.Log.Lua.Error(message);
        }
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
    public List<string> MissingRequires { get; } = [];
}
