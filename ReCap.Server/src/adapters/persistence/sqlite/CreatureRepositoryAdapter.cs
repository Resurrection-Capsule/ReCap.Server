using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer;

public class CreatureRepositoryAdapter
{
    private SqliteConfig sqliteConfig;
    private CreatureMapper creatureMapper;
    private Random sequenceRandomGenerator;

    public CreatureRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        creatureMapper = new CreatureMapper();
        sequenceRandomGenerator = new Random();
    }

    public List<CreatureModel> getCreaturesByAccountId(ulong accountId)
    {
        return sqliteConfig.Creatures.Where(b => b.AccountID == accountId).ToList();
    }

    public void insertCreature(Creature creature)
    {
        creature.ID = (ulong)sequenceRandomGenerator.Next(10000000); // TODO: Generate ID dynamically

        var creatureModel = creatureMapper.toModel(creature);
        sqliteConfig.Creatures.Add(creatureModel);
        sqliteConfig.SaveChanges();
    }
}
