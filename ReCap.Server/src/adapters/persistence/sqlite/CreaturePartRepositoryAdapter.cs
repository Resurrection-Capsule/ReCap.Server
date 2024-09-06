using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer;

public class CreaturePartRepositoryAdapter
{
    private static string CREATURE_PART_SEQUENCE_NAME = "CREATURE_PART_SEQUENCE";

    private SqliteConfig sqliteConfig;
    private CreaturePartMapper creaturePartMapper;
    private DbSequenceAdapter sequenceRandomGenerator;

    public CreaturePartRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        creaturePartMapper = new CreaturePartMapper();
        sequenceRandomGenerator = new DbSequenceAdapter(newSqliteConfig);
    }

    public List<CreaturePartModel> getCreaturePartsByAccountId(ulong accountId)
    {
        return sqliteConfig.CreatureParts.Where(b => b.AccountId == accountId).ToList();
    }

    public void insertCreaturePart(CreaturePart creaturePart)
    {
        creaturePart.ID = (ulong)sequenceRandomGenerator.Next(CREATURE_PART_SEQUENCE_NAME);

        var creaturePartModel = creaturePartMapper.toModel(creaturePart);
        sqliteConfig.CreatureParts.Add(creaturePartModel);
        sqliteConfig.SaveChanges();
    }

    public void insertCreatureParts(List<CreaturePart> creatureParts)
    {
        foreach (var creaturePart in creatureParts) {
            creaturePart.ID = (ulong)sequenceRandomGenerator.Next(CREATURE_PART_SEQUENCE_NAME);

            var creaturePartModel = creaturePartMapper.toModel(creaturePart);
            sqliteConfig.CreatureParts.Add(creaturePartModel);
        }
        sqliteConfig.SaveChanges();
    }
}
