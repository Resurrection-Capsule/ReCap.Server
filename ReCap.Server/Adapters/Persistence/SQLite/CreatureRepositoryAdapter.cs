using System;
using System.IO;
using System.Net;
using System.Text;
using ReCap.Server.Config;
using ReCap.Server.Domain;
using ReCap.Server.Mappers;
using ReCap.Server.Models;

namespace ReCap.Server.Adapters.Persistence.SQLite;

public class CreatureRepositoryAdapter
{
    private static string CREATURE_SEQUENCE_NAME = "CREATURE_SEQUENCE";

    private SqliteConfig sqliteConfig;
    private CreatureMapper creatureMapper;
    private DbSequenceAdapter sequenceRandomGenerator;

    public CreatureRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        creatureMapper = new CreatureMapper();
        sequenceRandomGenerator = new DbSequenceAdapter(newSqliteConfig);
    }

    public CreatureModel getCreatureById(ulong creatureId)
    {
        return sqliteConfig.Creatures.SingleOrDefault(b => b.ID == creatureId);
    }

    public List<CreatureModel> getCreaturesByAccountId(ulong accountId)
    {
        return sqliteConfig.Creatures.Where(b => b.AccountID == accountId).ToList();
    }

    public CreatureModel insertCreature(Creature creature)
    {
        creature.ID = (ulong)sequenceRandomGenerator.Next(CREATURE_SEQUENCE_NAME);

        var creatureModel = creatureMapper.toModel(creature);
        sqliteConfig.Creatures.Add(creatureModel);
        sqliteConfig.SaveChanges();

        return creatureModel;
    }

    public void updateCreature(CreatureModel creatureModel)
    {
        sqliteConfig.SaveChanges();
    }
}
