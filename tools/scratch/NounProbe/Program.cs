using System;
using System.Linq;
using AssetData.Parser;
using AssetData.Parser.Model;

// Dumps noun fields relevant to the client render gate (FUN_009ec530:
// noun.isFixed && noun.physicsType != 0 && obj.hasCollision) for the invisible
// obelisks vs a known-rendering enemy noun.
string packagePath = args.Length > 0 ? args[0] : @"C:\CodingProjects\Personal\Darkspore\Data\AssetData_Binary.package";

using var reader = new DbpfReader(packagePath);
var parser = new AssetParser();

string[] targets =
{
    "prefab_health_obelisk",
    "prefab_boss_obelisk",
    "ZelemBasicHybrid",
    "SecurityTeleporter"
};

foreach (var (name, entry) in reader.ListAssetsByType("Noun"))
{
    var bare = name.LastIndexOf('.') > 0 ? name[..name.LastIndexOf('.')] : name;
    if (!targets.Contains(bare, StringComparer.OrdinalIgnoreCase)) continue;

    var data = reader.ReadEntry(entry);
    var fileType = parser.GetFileType("Noun");
    if (data is null || fileType is null) { Console.WriteLine($"{bare}: unreadable"); continue; }

    var noun = parser.Parse(data, fileType.RootStruct, fileType.HeaderSize);
    Console.WriteLine($"=== {bare} ===");
    Dump(noun, 0, maxDepth: 2);
}

static void Dump(AssetValue node, int depth, int maxDepth)
{
    var indent = new string(' ', depth * 2);
    var text = node switch
    {
        StringValue s => $"\"{s.Value}\"",
        _ => node.GetType().GetProperty("Value")?.GetValue(node)?.ToString() ?? node.Kind.ToString()
    };
    Console.WriteLine($"{indent}{node.Name}: {text}");
    if (depth >= maxDepth) return;
    foreach (var child in node.Children)
        Dump(child, depth + 1, maxDepth);
}
