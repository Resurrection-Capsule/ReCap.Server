using System;
using System.Linq;
using AssetData.Parser;
using AssetData.Parser.Model;

// Probes the PlayerClass ability slot fields (basicAbility/specialAbility1-3/passiveAbility)
// to learn their parsed shape (StringValue name vs NumberValue hash) for the 0x9C cast wiring.
string packagePath = args.Length > 0 ? args[0] : @"C:\CodingProjects\Personal\Darkspore\Data\AssetData_Binary.package";

using var reader = new DbpfReader(packagePath);
var parser = new AssetParser();

string[] slotFields = ["basicAbility", "specialAbility1", "specialAbility2", "specialAbility3", "passiveAbility"];

var dumped = 0;
foreach (var (name, entry) in reader.ListAssetsByType("PlayerClass"))
{
    var data = reader.ReadEntry(entry);
    var fileType = parser.GetFileType("PlayerClass");
    if (data is null || fileType is null) continue;

    var pc = parser.Parse(data, fileType.RootStruct, fileType.HeaderSize);
    Console.WriteLine($"=== {name} (instance 0x{entry.Key.InstanceId:X8}) ===");
    foreach (var field in slotFields)
    {
        var node = FindByName(pc, field);
        if (node is null) { Console.WriteLine($"  {field}: <absent>"); continue; }
        var detail = node switch
        {
            StringValue s => $"StringValue \"{s.Value}\" -> fnv 0x{WireHash.Fnv1a(Bare(s.Value)):X8}",
            NumberValue n => $"NumberValue {n.Value} (0x{(uint)n.Value:X8})",
            _ => $"{node.GetType().Name} kind={node.Kind}"
        };
        Console.WriteLine($"  {field}: {detail}");
    }
    if (++dumped >= 4) break;
}

static AssetValue? FindByName(AssetValue node, string name)
{
    if (string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase)) return node;
    foreach (var child in node.Children)
        if (FindByName(child, name) is { } hit) return hit;
    return null;
}

static string Bare(string s)
{
    var slash = s.LastIndexOfAny(['/', '\\']);
    var name = slash >= 0 ? s[(slash + 1)..] : s;
    var dot = name.LastIndexOf('.');
    return dot > 0 ? name[..dot] : name;
}
