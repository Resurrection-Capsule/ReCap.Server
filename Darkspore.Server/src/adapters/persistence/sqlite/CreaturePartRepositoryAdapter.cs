using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer;

public class CreaturePartRepositoryAdapter
{
    private SqliteConfig sqliteConfig;
    private CreaturePartMapper creaturePartMapper;
    private Random sequenceRandomGenerator;

    public CreaturePartRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        creaturePartMapper = new CreaturePartMapper();
        sequenceRandomGenerator = new Random();
    }

    public List<CreaturePartModel> getCreaturePartsByAccountId(ulong accountId)
    {
        return sqliteConfig.CreatureParts.Where(b => b.AccountId == accountId).ToList();
    }

    // public void insertCreaturePart(CreaturePart creaturePart)
    // {
    //     creaturePart.ID = (ulong)sequenceRandomGenerator.Next(10000000); // TODO: Generate ID dynamically

    //     var creaturePartModel = creaturePartMapper.toModel(creaturePart);
    //     sqliteConfig.CreatureParts.Add(creaturePartModel);
    //     sqliteConfig.SaveChanges();
    // }
}
