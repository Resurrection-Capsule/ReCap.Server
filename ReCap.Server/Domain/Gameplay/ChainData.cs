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
        SetLevelByIndex(1); // zelems_1 instead of tutorial

        EnemyNouns[0] = FnvHash("VerdanthBasicMelee.Noun");
        EnemyNouns[1] = FnvHash("ZelemBasicHybrid.Noun");
        EnemyNouns[2] = FnvHash("ZelemBasicPackMelee.Noun");
        EnemyNouns[3] = FnvHash("ZelemBasicRangedHoming.Noun");
        EnemyNouns[4] = FnvHash("ZelemBasicFlyingMelee.Noun");
    }

    public void PopulateFromLevel(ReCap.Server.Services.AssetDatabase db)
    {
        var levelAsset = db.GetAsset(LevelName + ".level");
        if (levelAsset == null) 
            return;

        var planetConfigKey = levelAsset["planetConfig"]?.DisplayValue;
        if (string.IsNullOrEmpty(planetConfigKey))
            return;

        var planetConfig = db.GetAsset(planetConfigKey);
        if (planetConfig == null)
            return;

        var minions = planetConfig["minion"]?.Elements;
        var specials = planetConfig["special"]?.Elements;
        
        int enemyIndex = 0;
        void AddToEnemyNouns(System.Collections.Generic.IEnumerable<AssetData.Parser.AssetNode>? elements)
        {
            if (elements == null) return;
            foreach (var el in elements)
            {
                if (enemyIndex >= EnemyNouns.Length) break;
                var nounNode = el["mpNoun"] as AssetData.Parser.StringNode;
                if (nounNode != null && !string.IsNullOrEmpty(nounNode.Value))
                {
                    if (!nounNode.Value.Contains("_"))
                    {
                        EnemyNouns[enemyIndex++] = FnvHash(nounNode.Value);
                    }
                }
            }
        }

        AddToEnemyNouns(minions);
        AddToEnemyNouns(specials);

        var bosses = planetConfig["boss"]?.Elements;
        var agents = planetConfig["agent"]?.Elements;
        
        int levelIndex = 0;
        void AddToLevelNouns(System.Collections.Generic.IEnumerable<AssetData.Parser.AssetNode>? elements)
        {
            if (elements == null) return;
            foreach (var el in elements)
            {
                if (levelIndex >= LevelNouns.Length) break;
                var nounNode = el["mpNoun"] as AssetData.Parser.StringNode;
                if (nounNode != null && !string.IsNullOrEmpty(nounNode.Value))
                {
                    if (!nounNode.Value.Contains("_"))
                    {
                        LevelNouns[levelIndex++] = FnvHash(nounNode.Value);
                    }
                }
            }
        }

        AddToLevelNouns(bosses);
        AddToLevelNouns(agents);
    }

    public string LevelName => LevelIndex < LevelNames.Length ? LevelNames[LevelIndex] : LevelNames[0];

    public uint MarkerSet => FnvHash($"{LevelName}_ai_1.Markerset");

    public uint MinorDifficulty => LevelIndex > 0 ? ((LevelIndex - 1) % 4) + 1 : 0;
    public uint MajorDifficulty => LevelIndex > 0 ? ((LevelIndex - 1) / 4) + 1 : 0;

    public void SetLevelByIndex(int index)
    {
        if (index < 0 || index >= LevelNames.Length)
            return;

        LevelIndex = (uint)index;
        
        // The first level is the tutorial. Its asset name is Darkspore_Tutorial_cryos_1_v2,
        // but the UI localization hash it expects is purely "tutorial.Level"
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
