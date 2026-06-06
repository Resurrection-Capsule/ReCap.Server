using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Services.Scripting;
using Xunit.Abstractions;

namespace ReCap.Tests.Scripting;

public class ScriptEngineBootTests(ITestOutputHelper output)
{
    internal static string? FindDataDir() => FindDataDirShared();

    internal static string? FindDataDirShared()
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
        output.WriteLine($"Total={report.Total} Failures={report.Failures.Count} MissingRequires={report.MissingRequires.Count}");
        foreach (var m in report.MissingRequires)
            output.WriteLine($"  [retail-missing] {m}");
        var registry = ScriptContextRegistry.Get(rt.L)?.Registry;
        if (registry is not null)
        {
            foreach (ScriptKind kind in Enum.GetValues<ScriptKind>())
                output.WriteLine($"  registry[{kind}]={registry.Count(kind)}");
        }
        Assert.True(report.Total >= 1000, $"expected ~1018 chunks, executed {report.Total}");
        Assert.Empty(report.Failures);
        Assert.True(report.MissingRequires.Count <= 13,
            $"retail-missing grew: {report.MissingRequires.Count}\n{string.Join("\n", report.MissingRequires)}");
    }
}
