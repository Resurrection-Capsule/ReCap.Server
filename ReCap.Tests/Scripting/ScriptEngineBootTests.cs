using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Services.Scripting;

namespace ReCap.Tests.Scripting;

public class ScriptEngineBootTests
{
    private static string? FindDataDir()
    {
        var env = Environment.GetEnvironmentVariable("RECAP_GAME_DATA");
        if (env is not null && File.Exists(Path.Combine(env, "ServerData.package"))) return env;
        var known = @"C:\CodingProjects\Personal\Darkspore\Data";
        return File.Exists(Path.Combine(known, "ServerData.package")) ? known : null;
    }

    [Fact]
    public void BootsAllServerDataScripts()
    {
        var dataDir = FindDataDir();
        if (dataDir is null) return;
        var vfs = new ScriptVfs(new PackageMounts(dataDir));
        var engine = new ScriptEngine(vfs);
        using var rt = LuaRuntime.CreateSandboxedState(n => vfs.GetChunk(ScriptVfs.ParseReference(n)));
        var report = engine.ExecuteBootScripts(rt);
        Assert.True(report.Total >= 1000, $"expected ~1042 chunks, executed {report.Total}");
        Assert.True(report.Failures.Count < report.Total / 10,
            $"boot failures {report.Failures.Count}/{report.Total}:\n{string.Join("\n", report.Failures.Take(20))}");
    }
}
