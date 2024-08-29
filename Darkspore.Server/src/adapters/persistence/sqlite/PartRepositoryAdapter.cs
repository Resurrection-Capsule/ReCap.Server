using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer;

public class PartRepositoryAdapter
{
    private SqliteConfig sqliteConfig;
    private PartMapper partMapper;
    private Random sequenceRandomGenerator;

    public PartRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        partMapper = new PartMapper();
        sequenceRandomGenerator = new Random();
    }

    public List<PartModel> getPartsByAccountId(ulong accountId)
    {
        return sqliteConfig.Parts.Where(b => b.AccountId == accountId).ToList();
    }

    // public void insertPart(Part part)
    // {
    //     part.ID = (ulong)sequenceRandomGenerator.Next(10000000); // TODO: Generate ID dynamically

    //     var partModel = partMapper.toModel(part);
    //     sqliteConfig.Parts.Add(partModel);
    //     sqliteConfig.SaveChanges();
    // }
}
