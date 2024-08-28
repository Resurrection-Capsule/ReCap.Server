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

    public List<CreatureModel> getCreaturesByAccountId(ulong accountId)
    {
        return sqliteConfig.Creatures.Where(b => b.AccountID == accountId).ToList();
    }

    public void insertCreature(Creature creature)
    {
        var creatureModel = creatureMapper.toModel(creature);
        sqliteConfig.Creatures.Add(creatureModel);
        sqliteConfig.SaveChanges();
    }
}
