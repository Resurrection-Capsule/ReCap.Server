using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AssetData.Parser;

// Dumps retail lua chunks from ServerData.package to .luac files and disassembles them
// with the project's float-ABI luac.exe (-l -l). Usage: LuaChunkDump <name> [<name>...]
string dataDir = @"C:\CodingProjects\Personal\Darkspore\Data";
string luacExe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\..\native\lua51\out\luac.exe"));
string outDir = Path.Combine(Path.GetTempPath(), "recap-luachunks");
Directory.CreateDirectory(outDir);

using var reader = new DbpfReader(Path.Combine(dataDir, "ServerData.package"));

// Scan mode: --scan <ascii> lists every chunk whose bytecode contains the string constant.
if (args.Length == 2 && args[0] == "--scan")
{
    var needle = System.Text.Encoding.ASCII.GetBytes(args[1]);
    foreach (var (scanName, scanEntry) in reader.ListAssetsByType("lua"))
    {
        var bytes = reader.ReadEntry(scanEntry);
        if (bytes is null) continue;
        for (int i = 0; i <= bytes.Length - needle.Length; i++)
        {
            bool hit = true;
            for (int j = 0; j < needle.Length; j++) if (bytes[i + j] != needle[j]) { hit = false; break; }
            if (hit) { Console.WriteLine($"{scanName}  (group 0x{scanEntry.Key.GroupId:X8} inst 0x{scanEntry.Key.InstanceId:X8})"); break; }
        }
    }
    return;
}

var wanted = args.Length > 0 ? args : new[] { "Fireball", "template_ability_projectile" };
var byInstance = new System.Collections.Generic.Dictionary<uint, string>();
foreach (var name in wanted)
{
    // "0x"-prefixed args are raw instance ids; anything else is FNV-hashed.
    if (name.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
        uint.TryParse(name[2..], System.Globalization.NumberStyles.HexNumber, null, out var raw))
        byInstance[raw] = name;
    else
        byInstance[WireHash.Fnv1a(name)] = name;
}

foreach (var (name, entry) in reader.ListAssetsByType("lua"))
{
    if (!byInstance.TryGetValue(entry.Key.InstanceId, out var wantedName)) continue;
    var data = reader.ReadEntry(entry);
    if (data is null) { Console.WriteLine($"{wantedName}: unreadable"); continue; }

    var path = Path.Combine(outDir, wantedName + ".luac");
    File.WriteAllBytes(path, data);
    Console.WriteLine($"=== {wantedName} ({data.Length} bytes, group 0x{entry.Key.GroupId:X8}) ===");

    var psi = new ProcessStartInfo(luacExe, $"-l -l -p \"{path}\"")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    using var p = Process.Start(psi)!;
    Console.WriteLine(p.StandardOutput.ReadToEnd());
    var err = p.StandardError.ReadToEnd();
    if (err.Length > 0) Console.WriteLine("STDERR: " + err);
    p.WaitForExit();
}
