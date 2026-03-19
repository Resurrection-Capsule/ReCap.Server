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

    public IEnumerable<string> ListAssets() => _reader.ListAssets();

    public void Dispose() => _reader.Dispose();
}
