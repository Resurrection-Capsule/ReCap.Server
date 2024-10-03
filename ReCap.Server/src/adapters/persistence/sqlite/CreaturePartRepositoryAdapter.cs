namespace ReCap.Server.Adapters.Persistence.SQLite.CreaturePartRepository;

using System;
using System.IO;
using System.Net;
using System.Text;

using ReCap.Server.Adapters.Persistence.SQLite.DbSequence;
using ReCap.Server.Config.Sqlite;
using ReCap.Server.Mapper.CreaturePart;
using ReCap.Server.Model.CreaturePart;

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

    public CreaturePartModel getCreaturePartById(ulong id)
    {
        return sqliteConfig.CreatureParts.SingleOrDefault(b => b.ID == id);
    }

    public List<CreaturePartModel> getCreaturePartsByAccountId(ulong accountId)
    {
        return sqliteConfig.CreatureParts.Where(b => b.AccountId == accountId).ToList();
    }

    public void insertCreaturePart(CreaturePartModel creaturePart)
    {
        creaturePart.ID = (ulong)sequenceRandomGenerator.Next(CREATURE_PART_SEQUENCE_NAME);
        sqliteConfig.CreatureParts.Add(creaturePart);
        sqliteConfig.SaveChanges();
    }

    public void insertCreatureParts(List<CreaturePartModel> creatureParts)
    {
        ulong sequence = (ulong)sequenceRandomGenerator.Next(CREATURE_PART_SEQUENCE_NAME, creatureParts.Count);
        foreach (var creaturePart in creatureParts) {
            creaturePart.ID = sequence++;
            sqliteConfig.CreatureParts.Add(creaturePart);
        }
        sqliteConfig.SaveChanges();
    }

    public void updateCreaturePart(CreaturePartModel creaturePartModel) {
        sqliteConfig.SaveChanges();
    }

    public void updateCreatureParts(List<CreaturePartModel> creaturePartModels) {
        sqliteConfig.SaveChanges();
    }

    public void deleteCreaturePart(CreaturePartModel creaturePartModel) {
        sqliteConfig.CreatureParts.Remove(creaturePartModel);
        sqliteConfig.SaveChanges();
    }
}
