using System.Diagnostics;

namespace ReCap.Tests.TestSupport;

public static class LuaFixtures
{
    public static byte[] Compile(string luaSource, bool strip = true)
    {
        var root = FindRepoRoot();
        var luac = Path.Combine(root, "native", "lua51", "out", "luac.exe");
        if (!File.Exists(luac))
            throw new InvalidOperationException($"luac.exe missing at {luac} - run native/lua51/build.ps1");
        var src = Path.Combine(Path.GetTempPath(), $"fix_{Guid.NewGuid():N}.lua");
        var outFile = Path.ChangeExtension(src, ".luac");
        File.WriteAllText(src, luaSource);
        try
        {
            var stripFlag = strip ? "-s " : "";
            var p = Process.Start(new ProcessStartInfo(luac, $"{stripFlag}-o \"{outFile}\" \"{src}\"") { RedirectStandardError = true })!;
            if (!p.WaitForExit(30_000))
            {
                p.Kill(entireProcessTree: true);
                throw new InvalidOperationException("luac timed out");
            }
            if (p.ExitCode != 0)
                throw new InvalidOperationException($"luac failed: {p.StandardError.ReadToEnd()}");
            return File.ReadAllBytes(outFile);
        }
        finally
        {
            File.Delete(src);
            if (File.Exists(outFile)) File.Delete(outFile);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "native", "lua51")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
