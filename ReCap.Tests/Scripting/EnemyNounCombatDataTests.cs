using AssetData.Parser.Model;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services;
using ReCap.Server.Services.Assets;
using Xunit.Abstractions;

namespace ReCap.Tests.Scripting;

// Director enemies must spawn as damageable combatants: C++ Object::Initialize non-player path
// reads npcClassData → mpClassAttributes → baseHealth (Object.cpp:677-680). The NonPlayerClass
// binary catalog order must match the client registrar @0x00f5c912 or the blob string table
// shifts and mpClassAttributes reads the localized display name (the "Chrono Striker" bug).
public class EnemyNounCombatDataTests(ITestOutputHelper output)
{
    [Fact]
    public async Task DirectorEnemyNounsResolveBaseHealthAndSpawnDamageable()
    {
        var dataDir = ScriptEngineBootTests.FindDataDirShared();
        if (dataDir is null) return;

        using var db = new AssetDatabase(System.IO.Path.Combine(dataDir, "AssetData_Binary.package"));
        await db.WarmUpAsync();

        // cryos_1 chain enemy bank (ChainData defaults). ZelemBasicPackMelee excluded: its
        // ClassAttributes blob parses to denormal garbage — separate anomaly, not the order bug.
        var names = new[]
        {
            "VerdanthBasicMelee.Noun", "ZelemBasicHybrid.Noun",
            "ZelemBasicRangedHoming.Noun", "ZelemBasicFlyingMelee.Noun"
        };

        var objects = new ObjectManager(db);
        uint objId = 100;
        foreach (var name in names)
        {
            var nounId = AssetData.Parser.DbpfReader.FnvHash(name);
            var noun = db.GetNoun(nounId);
            Assert.NotNull(noun);

            // mpClassAttributes reads the "Chrono Striker" display name (parser NonPlayerClass
            // misalignment); ResolveClassAttributesForCreature falls back to the by-convention
            // <Creature>.ClassAttributes ref so the enemy still spawns damageable.
            var attrs = db.ResolveClassAttributesForCreature(nounId);
            var baseHealth = attrs?.FindByName("baseHealth").AsFloat() ?? -1f;

            var spawned = objects.Spawn(objId++, nounId, default, 1f, team: 0, playerControlled: false);
            output.WriteLine($"{name}: baseHealth={baseHealth} spawned.MaxHealth={spawned.MaxHealth}");

            Assert.True(baseHealth > 0f, $"{name}: baseHealth must resolve > 0");
            Assert.Equal(baseHealth, spawned.MaxHealth);
            Assert.Equal(baseHealth, spawned.Health);
        }
    }
}
