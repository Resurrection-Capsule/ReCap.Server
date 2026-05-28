using AssetData.Parser.Model;
using ReCap.Server.Services.Assets;

namespace ReCap.Server.Domain.Gameplay;

public class ChainData
{
    public static readonly string[] LevelNames =
    [
        "Darkspore_Tutorial_cryos_1_v2",
        "zelems_1", "zelems_3", "nocturna_4", "nocturna_1",
        "verdanth_1", "verdanth_3", "zelems_2", "zelems_4",
        "cryos_4", "cryos_3", "verdanth_2", "verdanth_4",
        "infinity_2", "infinity_3", "cryos_1", "cryos_2",
        "nocturna_3", "nocturna_2", "infinity_1", "infinity_4",
        "scaldron_1", "scaldron_2", "scaldron_3", "scaldron_4"
    ];

    public uint Level { get; set; }
    public uint LevelIndex { get; set; }
    public uint StarLevel { get; set; }
    public byte Progression { get; set; }
    public bool CompletedLevel { get; set; }

    public uint[] EnemyNouns { get; } = new uint[6];
    public uint[] LevelNouns { get; } = new uint[2];

    public ChainData()
    {
        SetLevelByIndex(1);

        EnemyNouns[0] = FnvHash("VerdanthBasicMelee.Noun");
        EnemyNouns[1] = FnvHash("ZelemBasicHybrid.Noun");
        EnemyNouns[2] = FnvHash("ZelemBasicPackMelee.Noun");
        EnemyNouns[3] = FnvHash("ZelemBasicRangedHoming.Noun");
        EnemyNouns[4] = FnvHash("ZelemBasicFlyingMelee.Noun");
    }

    public void PopulateFromLevel(ReCap.Server.Services.AssetDatabase db)
    {
        var levelAsset = db.GetLevel(LevelName);
        if (levelAsset == null)
            return;

        var planetConfigKey = (levelAsset.FindByName("planetConfig") as StringValue)?.Value;
        if (string.IsNullOrEmpty(planetConfigKey))
            return;

        var planetConfig = db.GetAssetByName(planetConfigKey);
        if (planetConfig == null)
            return;

        int enemyIndex = 0;
        void AddToEnemyNouns(ArrayValue? array)
        {
            if (array == null) return;
            foreach (var el in array.Items)
            {
                if (enemyIndex >= EnemyNouns.Length) break;
                if (el.FindByName("mpNoun") is not StringValue noun || string.IsNullOrEmpty(noun.Value)) continue;
                if (noun.Value.Contains('_')) continue;
                EnemyNouns[enemyIndex++] = FnvHash(noun.Value);
            }
        }

        AddToEnemyNouns(planetConfig.FindByName("minion") as ArrayValue);
        AddToEnemyNouns(planetConfig.FindByName("special") as ArrayValue);

        int levelIndex = 0;
        void AddToLevelNouns(ArrayValue? array)
        {
            if (array == null) return;
            foreach (var el in array.Items)
            {
                if (levelIndex >= LevelNouns.Length) break;
                if (el.FindByName("mpNoun") is not StringValue noun || string.IsNullOrEmpty(noun.Value)) continue;
                if (noun.Value.Contains('_')) continue;
                LevelNouns[levelIndex++] = FnvHash(noun.Value);
            }
        }

        AddToLevelNouns(planetConfig.FindByName("boss") as ArrayValue);
        AddToLevelNouns(planetConfig.FindByName("agent") as ArrayValue);
    }

    public string LevelName => LevelIndex < LevelNames.Length ? LevelNames[LevelIndex] : LevelNames[0];

    private uint? _resolvedMarkerSet;

    public uint MarkerSet => _resolvedMarkerSet ?? FnvHash($"{LevelName}_ai_1.Markerset");

    public void ResolveMarkerSet(ReCap.Server.Services.AssetDatabase db)
    {
        _resolvedMarkerSet = db.GetAIMarkerSetHash(LevelName);
    }

    public uint MinorDifficulty => LevelIndex > 0 ? ((LevelIndex - 1) % 4) + 1 : 0;
    public uint MajorDifficulty => LevelIndex > 0 ? ((LevelIndex - 1) / 4) + 1 : 0;

    public void SetLevelByIndex(int index)
    {
        if (index < 0 || index >= LevelNames.Length)
            return;

        LevelIndex = (uint)index;
        _resolvedMarkerSet = null;

        if (index == 0)
        {
            Level = FnvHash("tutorial.Level");
        }
        else
        {
            Level = FnvHash($"{LevelNames[index]}.Level");
        }
    }

    public void SetEnemyNoun(string nounStr, int index)
    {
        if (index < EnemyNouns.Length)
            EnemyNouns[index] = FnvHash(nounStr);
    }

    public void SetLevelNoun(string nounStr, int index)
    {
        if (index < LevelNouns.Length)
            LevelNouns[index] = FnvHash(nounStr);
    }

    public static uint FnvHash(string s)
    {
        uint hash = 0x811C9DC5;
        foreach (char c in s.ToLowerInvariant())
        {
            hash *= 0x1000193;
            hash ^= (byte)c;
        }
        return hash;
    }
}
