using System.Text.Json;

namespace ReCap.Server.Services;

public sealed record GameInstall(string Root, string DataDir);

public static class GameInstallLocator
{
    public static GameInstall? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = Path.GetFullPath(path.Trim('"', '\''));
        if (File.Exists(path))
            path = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(path)) return null;
        foreach (var candidate in new[] { path, Path.Combine(path, "Data"), Path.GetDirectoryName(path) ?? path })
        {
            var dataDir = Path.GetFileName(candidate).Equals("Data", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : Path.Combine(candidate, "Data");
            if (File.Exists(Path.Combine(dataDir, "AssetData_Binary.package")) &&
                File.Exists(Path.Combine(dataDir, "ServerData.package")))
                return new GameInstall(Path.GetDirectoryName(dataDir)!, dataDir);
        }
        return null;
    }

    public static GameInstall? Resolve(string? cliPath, string persistencePath, IReadOnlyList<string>? probePaths = null, bool skipRegistry = false)
    {
        var fromCli = Normalize(cliPath);
        if (fromCli is not null) { Persist(persistencePath, fromCli.Root); return fromCli; }

        if (File.Exists(persistencePath))
        {
            var saved = TryReadPersisted(persistencePath);
            var fromSaved = Normalize(saved);
            if (fromSaved is not null) return fromSaved;
        }

        if (!skipRegistry)
        {
            var fromRegistry = Normalize(ProbeRegistry());
            if (fromRegistry is not null) { Persist(persistencePath, fromRegistry.Root); return fromRegistry; }
        }

        foreach (var probe in probePaths ?? DefaultProbePaths())
        {
            var hit = Normalize(probe);
            if (hit is not null) { Persist(persistencePath, hit.Root); return hit; }
        }
        return null;
    }

    public static void Persist(string persistencePath, string root)
    {
        try { File.WriteAllText(persistencePath, JsonSerializer.Serialize(new PersistedPath(root))); }
        catch (Exception ex) { Util.Logging.Log.Server.Warn($"game-path persist failed: {ex.Message}"); }
    }

    private static string? TryReadPersisted(string persistencePath)
    {
        try { return JsonSerializer.Deserialize<PersistedPath>(File.ReadAllText(persistencePath))?.Root; }
        catch { return null; }
    }

    private static string? ProbeRegistry()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            foreach (var keyPath in new[]
            {
                @"SOFTWARE\WOW6432Node\Electronic Arts\Darkspore",
                @"SOFTWARE\Electronic Arts\Darkspore",
            })
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
                if (key?.GetValue("InstallDir") is string dir && dir.Length > 0) return dir;
            }
            using var uninstall = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
            foreach (var sub in uninstall?.GetSubKeyNames() ?? [])
            {
                using var k = uninstall!.OpenSubKey(sub);
                if (k?.GetValue("DisplayName") is string n && n.Contains("Darkspore", StringComparison.OrdinalIgnoreCase)
                    && k.GetValue("InstallLocation") is string loc && loc.Length > 0)
                    return loc;
            }
        }
        catch { }
        return null;
    }

    private static IReadOnlyList<string> DefaultProbePaths() =>
    [
        @"C:\Program Files (x86)\Origin Games\Darkspore",
        @"C:\Program Files (x86)\Electronic Arts\Darkspore",
        @"C:\Program Files (x86)\Steam\steamapps\common\Darkspore",
        @"C:\Games\Darkspore",
    ];

    private sealed record PersistedPath(string Root);
}
