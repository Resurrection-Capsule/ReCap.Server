using AssetData.Parser;

namespace ReCap.Server.Services.Assets;

public sealed class EntryIndex
{
    private readonly Dictionary<(uint TypeId, uint InstanceId), DbpfEntry> _byTypeInstance;
    private readonly Dictionary<uint, List<DbpfEntry>> _byType;

    public int Count => _byTypeInstance.Count;

    public EntryIndex(DbpfReader reader)
    {
        _byTypeInstance = new Dictionary<(uint, uint), DbpfEntry>(reader.Entries.Count);
        _byType = new Dictionary<uint, List<DbpfEntry>>();

        foreach (var entry in reader.Entries)
        {
            var key = (entry.Key.TypeId, entry.Key.InstanceId);
            _byTypeInstance.TryAdd(key, entry);

            if (!_byType.TryGetValue(entry.Key.TypeId, out var bucket))
                _byType[entry.Key.TypeId] = bucket = new List<DbpfEntry>();
            bucket.Add(entry);
        }
    }

    public bool TryGet(uint typeId, uint instanceId, out DbpfEntry entry) =>
        _byTypeInstance.TryGetValue((typeId, instanceId), out entry);

    public IReadOnlyList<DbpfEntry> ByType(uint typeId) =>
        _byType.TryGetValue(typeId, out var bucket) ? bucket : System.Array.Empty<DbpfEntry>();

    public IReadOnlyList<DbpfEntry> ByType(string typeExtension) =>
        ByType(DbpfReader.FnvHash(typeExtension));
}
