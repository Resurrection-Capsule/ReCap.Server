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

    public CreatureRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        creatureMapper = new CreatureMapper();
    }

    public List<Creature> getCreaturesByAccountId(ulong accountId)
    {
        var creatureModels = sqliteConfig.Creatures.Where(b => b.AccountID == accountId).ToList();
        return creatureModels.Select(creatureModel => creatureMapper.toDomain(creatureModel)).ToList();
    }

    public void saveCreature(Creature creature)
    {
        var creatureModel = creatureMapper.toModel(creature);
        sqliteConfig.Creatures.Add(creatureModel);
        sqliteConfig.SaveChanges();
    }
}
