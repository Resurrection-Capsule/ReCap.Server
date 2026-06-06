using System.Diagnostics;
using System.Text;
using ReCap.Server.Adapters.Scripting;

namespace ReCap.Server.Services.Scripting;

public sealed class ScriptEngine(ScriptVfs vfs)
{
    private static readonly uint[] BootGroupOrder =
    [
        0x3681D755, 0xDA09176B, 0xFC0FF8F5, 0xD2FCB262, 0x7153BBB1,
        0xD79FA88C, 0xC130A42A, 0xB2A79C5C, 0xEE84D09A, 0x24F78AA1,
    ];

    // FNV-verified boot-group names (6 of 10 unresolved): docs/architecture/research/LUA_REGISTRAR_TABLES.md
    private static readonly Dictionary<uint, string> BootGroupNames = new()
    {
        [0x3681D755] = "lua",
        [0xFC0FF8F5] = "modifiers",
        [0x7153BBB1] = "abilities",
        [0xC130A42A] = "behaviors",
    };

    // global.lua defines the Class OOP helper required by position-13 chunks (C6)
    private static readonly (uint Group, uint Instance)[] PreBootChunks =
    [
        (0x3681D755, 0x57572DAC),
    ];

    public BootReport ExecuteBootScripts(LuaRuntime runtime)
    {
        var report = new BootReport();
        var executed = new HashSet<(uint, uint)>();
        var groupCounts = new Dictionary<uint, int>();
        var sw = Stopwatch.StartNew();

        foreach (var (g, i) in PreBootChunks)
        {
            var bytes = vfs.GetChunk(new ScriptKey(g, i, 0));
            if (bytes is null) continue;
            report.Total++;
            executed.Add((g, i));
            groupCounts[g] = groupCounts.GetValueOrDefault(g) + 1;
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
                groupCounts[group] = groupCounts.GetValueOrDefault(group) + 1;
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

        sw.Stop();
        report.ElapsedMs = sw.ElapsedMilliseconds;
        foreach (var group in BootGroupOrder)
            report.Groups.Add((BootGroupNames.GetValueOrDefault(group, $"0x{group:X8}"), groupCounts.GetValueOrDefault(group)));
        return report;
    }

    private static void ClassifyFailure(BootReport report, string message)
    {
        if (message.Contains("require: chunk not found"))
        {
            var missing = MissingRequire.Parse(message);
            report.MissingRequires.Add(missing);
            Util.Logging.Log.Lua.Debug($"[retail-missing] {missing}");
        }
        else
        {
            report.Failures.Add(message);
            Util.Logging.Log.Lua.Error(message);
        }
    }

    public LuaRuntime CreateBootedRuntime() => CreateBootedRuntime("boot");

    public LuaRuntime CreateBootedRuntime(string contextTag)
    {
        var runtime = LuaRuntime.CreateSandboxedState(name => vfs.GetChunk(ScriptVfs.ParseReference(name)), contextTag);
        var report = ExecuteBootScripts(runtime);
        Util.Logging.Log.Lua.Info($"[{contextTag}] {report.OneLine()}");
        return runtime;
    }
}

public sealed class BootReport
{
    public int Total { get; set; }
    public long ElapsedMs { get; set; }
    public List<(string Label, int Count)> Groups { get; } = [];
    public List<string> Failures { get; } = [];
    public List<MissingRequire> MissingRequires { get; } = [];

    public string OneLine() =>
        $"{Total} chunks in {ElapsedMs}ms ({Failures.Count} failures, {MissingRequires.Count} retail-missing)";

    public string FormatSummaryBlock()
    {
        var width = Groups.Count == 0 ? 1 : Groups.Max(g => g.Count.ToString().Length);
        var sb = new StringBuilder();
        sb.AppendLine(Failures.Count == 0 ? "✓ Boot" : "✗ Boot");
        foreach (var (label, count) in Groups)
            sb.AppendLine($"    [{count.ToString().PadLeft(width)}] {label}");
        sb.AppendLine("    ─────────────────────");
        sb.Append($"    → {OneLine()}");
        return sb.ToString();
    }

    public string FormatMissingBlock()
    {
        var grouped = MissingRequires
            .GroupBy(m => m.Chunk)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();
        var sb = new StringBuilder();
        sb.Append($"Retail-missing requires: {MissingRequires.Count} refs → {grouped.Count} chunks");
        foreach (var g in grouped)
            sb.Append($"\n    {g.Count()}× {g.Key}");
        return sb.ToString();
    }
}

public readonly record struct MissingRequire(string Caller, string Chunk)
{
    public static MissingRequire Parse(string message)
    {
        var caller = "";
        var rest = message;
        if (rest.StartsWith('['))
        {
            var close = rest.IndexOf(']');
            if (close > 0)
            {
                caller = rest[1..close];
                rest = rest[(close + 1)..];
            }
        }
        const string marker = "chunk not found: ";
        var at = rest.IndexOf(marker, StringComparison.Ordinal);
        var chunk = at >= 0 ? rest[(at + marker.Length)..] : rest;
        var lineEnd = chunk.IndexOf('\n');
        if (lineEnd >= 0) chunk = chunk[..lineEnd];
        return new MissingRequire(caller, chunk.Trim());
    }

    public override string ToString() => $"{Caller} → {Chunk}";
}
