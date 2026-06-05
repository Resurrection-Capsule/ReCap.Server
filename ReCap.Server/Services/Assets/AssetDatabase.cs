using System.Diagnostics;
using System.Text;
using ReCap.Server.Util.Logging;
using AssetData.Parser;
using AssetData.Parser.Model;
using ReCap.Server.Config;
using ReCap.Server.Services.Assets;
using ReCap.Server.Services.Assets.Loaders;
using ReCap.Server.Util;

namespace ReCap.Server.Services;

public sealed class AssetDatabase : IDisposable
{
    private readonly string _packagePath;
    private DbpfReader? _reader;
    private AssetParser? _parser;
    private Assets.EntryIndex? _entryIndex;

    private readonly Dictionary<uint, AssetValue> _nouns = new();
    private readonly Dictionary<uint, AssetValue> _nounsByWireId = new();
    private readonly Dictionary<uint, AssetValue> _nonPlayerClasses = new();
    private readonly Dictionary<uint, AssetValue> _playerClasses = new();
    private readonly Dictionary<uint, AssetValue> _npcAffixes = new();
    private readonly Dictionary<uint, AssetValue> _classAttributes = new();
    private readonly Dictionary<uint, AssetValue> _aiDefinitions = new();
    private readonly Dictionary<uint, AssetValue> _abilities = new();
    private readonly Dictionary<string, AssetValue> _levelsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, AssetValue> _markerSets = new();

    private readonly Dictionary<string, uint> _aiMarkerSetByLevel = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AssetValue> _byVirtualName = new(StringComparer.OrdinalIgnoreCase);

    public bool IsReady { get; private set; }
    public string PackagePath => _packagePath;

    public AssetDatabase(string packagePath)
    {
        _packagePath = packagePath;
    }

    public static AssetDatabase FromConfig() => new(ServerConfig.GamePath);

    // Two hash domains (VERIFIED 2026-06-04): DBPF InstanceId = FNV(bare name, no extension);
    // the WIRE noun id = FNV(name + ".Noun"). GetNoun resolves both so wire-id callers
    // (squad nouns, ObjectManager.Spawn) actually hit data.
    public AssetValue? GetNoun(uint id) => _nouns.GetValueOrDefault(id) ?? _nounsByWireId.GetValueOrDefault(id);
    public AssetValue? GetNounByName(string name) => _nouns.GetValueOrDefault(DbpfReader.FnvHash(name));

    public AssetValue? GetNonPlayerClass(uint id) => _nonPlayerClasses.GetValueOrDefault(id);
    public AssetValue? GetPlayerClass(uint id) => _playerClasses.GetValueOrDefault(id);
    public AssetValue? GetNpcAffix(uint id) => _npcAffixes.GetValueOrDefault(id);
    public AssetValue? GetClassAttributes(uint id) => _classAttributes.GetValueOrDefault(id);
    public AssetValue? GetAIDefinition(uint id) => _aiDefinitions.GetValueOrDefault(id);
    public AssetValue? GetAbility(uint id) => _abilities.GetValueOrDefault(id);
    public AssetValue? GetAbilityByName(string name) => _abilities.GetValueOrDefault(DbpfReader.FnvHash(name));

    public AssetValue? GetLevel(string levelName) => _levelsByName.GetValueOrDefault(levelName);
    public AssetValue? GetMarkerSet(uint nameHash) => _markerSets.GetValueOrDefault(nameHash);
    public AssetValue? GetMarkerSetByName(string name) => _markerSets.GetValueOrDefault(DbpfReader.FnvHash(name));

    public uint? GetAIMarkerSetHash(string levelName) =>
        _aiMarkerSetByLevel.TryGetValue(levelName, out var hash) ? hash : null;

    public IReadOnlyDictionary<uint, AssetValue> Nouns => _nouns;
    public IReadOnlyDictionary<string, AssetValue> Levels => _levelsByName;
    public IReadOnlyDictionary<uint, AssetValue> MarkerSets => _markerSets;

    public AssetValue? GetAssetByName(string virtualName)
    {
        if (_reader is null || _parser is null) return null;
        if (_byVirtualName.TryGetValue(virtualName, out var cached)) return cached;

        var data = _reader.GetAsset(virtualName);
        if (data is null) return null;

        var ext = Path.GetExtension(virtualName).TrimStart('.');
        var fileType = _parser.GetFileType(ext);
        if (fileType is null) return null;

        var node = _parser.Parse(data, fileType.RootStruct, fileType.HeaderSize);
        _byVirtualName[virtualName] = node;
        return node;
    }

    public AssetValue? ResolveClassAttributesForCreature(uint nounId)
    {
        var noun = GetNoun(nounId);
        if (noun is null) return null;

        var classRef = (noun.FindByName("playerClassData") as StringValue)?.Value
                     ?? (noun.FindByName("npcClassData") as StringValue)?.Value;
        if (string.IsNullOrEmpty(classRef)) return null;

        var classAsset = GetAssetByName(classRef);
        if (classAsset is null) return null;

        var attrsRef = (classAsset.FindByName("mpClassAttributes") as StringValue)?.Value;
        if (string.IsNullOrEmpty(attrsRef)) return null;

        return GetAssetByName(attrsRef);
    }

    public Task WarmUpAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        try
        {
            WarmUp(ct);
        }
        catch (Exception ex)
        {
            Log.Assets.Error(ex, "Warm-up failed");
        }
    }, ct);

    private void WarmUp(CancellationToken ct)
    {
        Log.Assets.Info($"Warm-up starting: {_packagePath}");
        var sw = Stopwatch.StartNew();

        if (!File.Exists(_packagePath))
        {
            Log.Assets.Error($"Package not found: {_packagePath}");
            return;
        }

        _reader = new DbpfReader(_packagePath);
        _parser = new AssetParser();
        _entryIndex = new Assets.EntryIndex(_reader);
        Log.Assets.Info($"DBPF opened: {_reader.Entries.Count} entries, parser registered {_parser.SupportedTypes.Count()} struct types");


        ct.ThrowIfCancellationRequested();

        var ctx = new Assets.LoaderContext(_reader, _parser, _entryIndex);

        LoadStep("Level/Markerset", () => LevelLoader.Load(ctx, _levelsByName, _markerSets, _aiMarkerSetByLevel));
        ct.ThrowIfCancellationRequested();

        LoadCategory(ctx, "Noun", "Noun", _nouns);
        BuildNounWireIndex();
        ct.ThrowIfCancellationRequested();

        LoadCategory(ctx, "NonPlayerClass", "NonPlayerClass", _nonPlayerClasses);
        LoadCategory(ctx, "playerClass", "PlayerClass", _playerClasses);
        LoadCategory(ctx, "npcAffix", "NPCAffix", _npcAffixes);
        LoadCategory(ctx, "ClassAttributes", "ClassAttributes", _classAttributes);
        LoadCategory(ctx, "AIDefinition", "AIDefinition", _aiDefinitions);

        sw.Stop();
        IsReady = true;

        (string Label, int Count)[] categories =
        {
            ("Levels", _levelsByName.Count),
            ("Markersets", _markerSets.Count),
            ("Nouns", _nouns.Count),
            ("NpcClasses", _nonPlayerClasses.Count),
            ("PlayerClasses", _playerClasses.Count),
            ("ClassAttrs", _classAttributes.Count),
            ("AiDefs", _aiDefinitions.Count),
            ("Affixes", _npcAffixes.Count),
        };

        var width = categories.Max(c => c.Count.ToString().Length);
        var sb = new StringBuilder();
        sb.AppendLine("✓ Ready");
        foreach (var (label, count) in categories)
            sb.AppendLine($"    [{count.ToString().PadLeft(width)}] {label}");
        sb.AppendLine("    ─────────────────────");
        sb.Append($"    → {_entryIndex.Count} Entries in {sw.ElapsedMilliseconds}ms");
        Log.Assets.Info(sb.ToString());
    }

    private static void LoadStep(string label, Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            Log.Assets.Error($"Step '{label}' failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void BuildNounWireIndex()
    {
        if (_reader is null) return;
        foreach (var (name, entry) in _reader.ListAssetsByType("Noun"))
        {
            if (_nouns.TryGetValue(entry.Key.InstanceId, out var node))
                _nounsByWireId[DbpfReader.FnvHash(name)] = node;
        }
        Log.Assets.Info($"Noun wire-id index: {_nounsByWireId.Count} entries");
    }

    private static void LoadCategory(Assets.LoaderContext ctx, string typeExtension, string rootStruct, Dictionary<uint, AssetValue> sink)
    {
        try { CategoryLoader.Load(ctx, typeExtension, rootStruct, sink); }
        catch (Exception ex)
        {
            Log.Assets.Error($"Category '{typeExtension}' failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void Dispose() => _reader?.Dispose();
}
