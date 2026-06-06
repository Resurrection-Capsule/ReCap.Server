using AssetData.Parser;
using ReCap.Server.Adapters.Persistence;

namespace ReCap.Server.Adapters.Scripting;

public readonly record struct ScriptKey(uint GroupId, uint InstanceId, uint TypeId);

public sealed class ScriptVfs(PackageMounts mounts)
{
    private Dictionary<(uint Group, uint Instance), byte[]>? _chunks;
    private readonly Lock _gate = new();

    public static uint Hash(string s) => WireHash.Fnv1a(s);

    public static ScriptKey ParseReference(string reference)
    {
        var bang = reference.IndexOf('!');
        var group = bang >= 0 ? reference[..bang] : null;
        var rest = bang >= 0 ? reference[(bang + 1)..] : reference;
        var dot = rest.LastIndexOf('.');
        var name = dot >= 0 ? rest[..dot] : rest;
        var ext = dot >= 0 ? rest[(dot + 1)..] : "lua";
        uint groupId = 0u;
        if (group is not null)
        {
            if (group.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                uint.TryParse(group[2..], System.Globalization.NumberStyles.HexNumber, null, out var parsed))
                groupId = parsed;
            else
                groupId = Hash(group);
        }
        return new ScriptKey(groupId, Hash(name), Hash(ext));
    }

    public byte[]? GetChunk(ScriptKey key)
    {
        var chunks = EnsureIndex();
        if (chunks is null) return null;
        if (key.GroupId != 0)
            return chunks.TryGetValue((key.GroupId, key.InstanceId), out var exact) ? exact : null;
        foreach (var ((g, i), bytes) in chunks)
            if (i == key.InstanceId) return bytes;
        return null;
    }

    public IReadOnlyList<(uint Instance, byte[] Bytes)> GetGroup(uint groupId)
    {
        var chunks = EnsureIndex();
        if (chunks is null) return [];
        return chunks.Where(kv => kv.Key.Item1 == groupId)
                     .OrderBy(kv => kv.Key.Item2)
                     .Select(kv => (kv.Key.Item2, kv.Value))
                     .ToList();
    }

    private Dictionary<(uint, uint), byte[]>? EnsureIndex()
    {
        lock (_gate)
        {
            if (_chunks is not null) return _chunks;
            var reader = mounts.Get(WellKnownPackage.ServerData);
            if (reader is null) return null;
            var index = new Dictionary<(uint, uint), byte[]>();
            foreach (var (_, entry) in reader.ListAssetsByType("lua"))
            {
                var data = reader.ReadEntry(entry);
                if (data is null)
                {
                    Util.Logging.Log.Assets.Warn($"ScriptVfs: failed to decompress (0x{entry.Key.GroupId:X8}, 0x{entry.Key.InstanceId:X8}), skipped");
                    continue;
                }
                index[(entry.Key.GroupId, entry.Key.InstanceId)] = data;
            }
            Util.Logging.Log.Assets.Info($"ScriptVfs indexed {index.Count} lua chunks from ServerData.package");
            _chunks = index;
            return _chunks;
        }
    }
}
