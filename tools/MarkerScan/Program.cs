using System;
using System.Collections.Generic;
using System.Linq;
using AssetData.Parser;
using AssetData.Parser.Model;

string packagePath = args.Length > 0 ? args[0] : @"C:\CodingProjects\Personal\Darkspore\Data\AssetData_Binary.package";
string levelName = args.Length > 1 ? args[1] : "scaldron_4";

using var reader = new DbpfReader(packagePath);
var parser = new AssetParser();
Console.WriteLine($"package={packagePath} entries={reader.Entries.Count} level={levelName}");

var nounInstanceIds = new HashSet<uint>();
foreach (var (name, entry) in reader.ListAssetsByType("Noun"))
    nounInstanceIds.Add(entry.Key.InstanceId);
Console.WriteLine($"noun files: {nounInstanceIds.Count}");
foreach (var probe in new[] { "prefab_health_obelisk.Noun", "prefab_health_obelisk", "SecurityTeleporter.noun", "SecurityTeleporter" })
    Console.WriteLine($"  probe '{probe}' fnv=0x{DbpfReader.FnvHash(probe):X8} inNounInstanceIds={nounInstanceIds.Contains(DbpfReader.FnvHash(probe))}");

static AssetValue? Find(AssetValue? node, string name)
{
    if (node is null) return null;
    foreach (var child in node.Children)
        if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
            return child;
    return null;
}

AssetValue? ParseEntry(DbpfEntry entry, string rootStruct)
{
    var data = reader.ReadEntry(entry);
    if (data is null) return null;
    var fileType = parser.GetFileType(rootStruct);
    if (fileType is null) return null;
    return parser.Parse(data, fileType.RootStruct, fileType.HeaderSize);
}

AssetValue? level = null;
foreach (var (name, entry) in reader.ListAssetsByType("Level"))
{
    var bare = name.LastIndexOf('.') > 0 ? name[..name.LastIndexOf('.')] : name;
    if (string.Equals(bare, levelName, StringComparison.OrdinalIgnoreCase))
    {
        level = ParseEntry(entry, "Level");
        break;
    }
}
if (level is null) { Console.WriteLine("LEVEL NOT FOUND"); return; }

var markersetEntriesByName = new Dictionary<string, DbpfEntry>(StringComparer.OrdinalIgnoreCase);
foreach (var (name, entry) in reader.ListAssetsByType("Markerset"))
{
    var bare = name.LastIndexOf('.') > 0 ? name[..name.LastIndexOf('.')] : name;
    markersetEntriesByName[bare] = entry;
}

static string DescribeValue(AssetValue v, int depth)
{
    var label = string.IsNullOrEmpty(v.Name) ? v.Kind.ToString() : v.Name;
    if (depth <= 0 || v.Children.Count == 0)
        return v switch
        {
            NumberValue n => $"{label}={n.Value}",
            BoolValue b => $"{label}={(b.Value ? 1 : 0)}",
            StringValue s => $"{label}='{s.Value}'",
            _ => label
        };
    return $"{label}[{string.Join(",", v.Children.Select(c => DescribeValue(c, depth - 1)))}]";
}

if (Find(level, "markersets") is not ArrayValue msArr) { Console.WriteLine("NO markersets ARRAY"); return; }

int totalMarkers = 0, totalPassFilter = 0, totalUnresolvable = 0;
foreach (var msRef in msArr.Items.OfType<StructValue>())
{
    var refPath = (Find(msRef, "markersetAsset") as StringValue)?.Value;
    if (string.IsNullOrEmpty(refPath)) continue;

    var refBare = refPath.LastIndexOf('.') > 0 ? refPath[..refPath.LastIndexOf('.')] : refPath;
    if (!markersetEntriesByName.TryGetValue(refBare, out var msEntry))
    {
        Console.WriteLine($"\n== {refPath}: MARKERSET FILE NOT FOUND");
        continue;
    }

    AssetValue? ms = null;
    try { ms = ParseEntry(msEntry, "Markerset"); } catch (Exception ex) { Console.WriteLine($"\n== {refPath}: PARSE FAIL {ex.Message}"); continue; }
    if (Find(ms, "markers") is not ArrayValue markers) { Console.WriteLine($"\n== {refPath}: no markers array"); continue; }

    bool hashKeyOk = DbpfReader.FnvHash(refBare) == msEntry.Key.InstanceId;
    Console.WriteLine($"\n== {refPath}: {markers.Items.Count} markers  fnv(bare)==instanceId: {(hashKeyOk ? "YES" : "NO")}");
    totalMarkers += markers.Items.Count;

    var byNoun = markers.Items.GroupBy(m => (Find(m, "nounDef") as StringValue)?.Value ?? "<null>");

    foreach (var grp in byNoun.OrderByDescending(g => g.Count()))
    {
        var nounDef = grp.Key;
        var hash = DbpfReader.FnvHash(nounDef);
        bool resolvable = nounInstanceIds.Contains(hash);

        var sample = grp.First();
        var collisionVals = grp.Select(m => Find(m, "createWithCollision") switch
        {
            BoolValue b => b.Value ? "1" : "0",
            NumberValue n => n.Value.ToString(),
            null => "absent",
            var other => other.Kind.ToString()
        }).Distinct();

        var compData = Find(sample, "componentData");
        var compDesc = compData is null ? "absent" : DescribeValue(compData, 2);

        bool passFilter = grp.Any(m =>
            (Find(m, "createWithCollision") is BoolValue cb && cb.Value) ||
            Find(m, "componentData") is not null);
        if (passFilter) { totalPassFilter += grp.Count(); if (!resolvable) totalUnresolvable += grp.Count(); }

        Console.WriteLine($"  {grp.Count(),4}x {nounDef,-50} hash=0x{hash:X8} nounFile={(resolvable ? "YES" : "NO ")} collision={string.Join("/", collisionVals)} comp={compDesc}");
    }
}

Console.WriteLine($"\nTOTAL markers={totalMarkers} passCurrentFilter={totalPassFilter} ofWhichNounUnresolvable={totalUnresolvable}");
