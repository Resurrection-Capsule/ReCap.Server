using System.IO;
using System.Threading;
using AssetData.Parser;
using ReCap.Server.Config;

namespace ReCap.Server.Adapters.Persistence;

public sealed record WellKnownPackage(string RelativePath)
{
    public static readonly WellKnownPackage AssetDataBinary = new("AssetData_Binary.package");
    public static readonly WellKnownPackage ServerData = new("ServerData.package");
    public static readonly WellKnownPackage Web = new("Web.package");
    public static readonly WellKnownPackage LocaleTextEnUs = new(Path.Combine("Locale", "pt-br", "Text.package"));
}

public sealed class PackageMounts(string dataDir)
{
    private readonly Dictionary<string, DbpfReader?> _open = [];
    private readonly Lock _gate = new();

    public string DataDir { get; } = dataDir;

    private static PackageMounts? _initialized;
    private static readonly Lazy<PackageMounts?> _fromConfig = new(BuildDefault);
    public static PackageMounts? Default => _initialized ?? _fromConfig.Value;
    public static void Initialize(string dataDir) => _initialized = new PackageMounts(dataDir);

    private static PackageMounts? BuildDefault()
    {
        var gamePath = ServerConfig.GamePath;
        if (string.IsNullOrWhiteSpace(gamePath)) return null;
        var dataDir = Path.GetDirectoryName(gamePath);
        return dataDir is null ? null : new PackageMounts(dataDir);
    }

    public DbpfReader? Get(WellKnownPackage package)
    {
        lock (_gate)
        {
            if (_open.TryGetValue(package.RelativePath, out var cached))
                return cached;
            var path = Path.Combine(DataDir, package.RelativePath);
            DbpfReader? reader = null;
            if (File.Exists(path))
                reader = new DbpfReader(path);
            else
                Util.Logging.Log.Assets.Warn($"Package not found: {path}");
            _open[package.RelativePath] = reader;
            return reader;
        }
    }
}
