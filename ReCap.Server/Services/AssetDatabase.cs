using AssetData.Parser;
using ReCap.Server.Config;

namespace ReCap.Server.Services;

public sealed class AssetDatabase : IDisposable
{
    private readonly DbpfReader _reader;
    private readonly AssetParser _parser = new();
    private readonly Dictionary<string, AssetNode> _cache = new(StringComparer.OrdinalIgnoreCase);

    public AssetDatabase(string packagePath)
    {
        _reader = new DbpfReader(packagePath);
    }

    public static AssetDatabase FromConfig() => new(ServerConfig.GamePath);

    public AssetNode? GetAsset(string virtualName)
    {
        if (_cache.TryGetValue(virtualName, out var cached))
            return cached;

        var data = _reader.GetAsset(virtualName);
        if (data is null)
            return null;

        var ext = Path.GetExtension(virtualName).TrimStart('.');
        var fileType = _parser.GetFileType(ext);
        if (fileType is null)
            return null;

        var node = _parser.Parse(data, fileType.RootStruct, fileType.HeaderSize);
        _cache[virtualName] = node;
        return node;
    }

    public AssetNode? GetAssetById(ulong assetId, string extension)
    {
        uint instanceId = (uint)assetId;
        uint groupId = (uint)(assetId >> 32);
        uint typeHash = DbpfReader.FnvHash(extension);

        foreach (var entry in _reader.Entries)
        {
            if (entry.Key.InstanceId == instanceId && entry.Key.TypeId == typeHash)
            {
                // Optionally check groupId if it's non-zero
                if (groupId != 0 && entry.Key.GroupId != groupId) continue;

                string cacheKey = $"0x{assetId:X16}.{extension}";
                if (_cache.TryGetValue(cacheKey, out var cached))
                    return cached;

                var data = _reader.ReadEntry(entry);
                if (data == null) return null;

                var fileType = _parser.GetFileType(extension);
                if (fileType == null) return null;

                var node = _parser.Parse(data, fileType.RootStruct, fileType.HeaderSize);
                _cache[cacheKey] = node;
                return node;
            }
        }

        return null;
    }

    public IEnumerable<AssetNode> GetLevelMarkers(string levelName)
    {
        var levelNode = GetAsset(levelName);
        if (levelNode == null) yield break;

        var markersets = levelNode["markersets"]?.Elements;
        if (markersets == null) yield break;

        foreach (var markersetRef in markersets)
        {
            var assetId = markersetRef["markersetAsset"]?.AsUInt64() ?? 0;
            if (assetId == 0) continue;

            var markersetNode = GetAssetById(assetId, "markerset");
            if (markersetNode == null) continue;

            var markers = markersetNode["markers"]?.Elements;
            if (markers == null) continue;

            foreach (var marker in markers)
            {
                yield return marker;
            }
        }
    }

    public IEnumerable<string> ListAssets() => _reader.ListAssets();

    public void Dispose() => _reader.Dispose();
}
