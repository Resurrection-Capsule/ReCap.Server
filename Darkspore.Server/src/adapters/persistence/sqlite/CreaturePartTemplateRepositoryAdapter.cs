using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer;

public class CreaturePartTemplateRepositoryAdapter
{
    private SqliteConfig sqliteConfig;

    public CreaturePartTemplateRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
    }

    public List<CreaturePartTemplateModel> getAllTemplates()
    {
        return sqliteConfig.CreaturePartTemplates.ToList();
    }

    public CreaturePartTemplateModel getTemplateById(ulong id)
    {
        return sqliteConfig.CreaturePartTemplates.SingleOrDefault(b => b.rigblockAssetId == id);
    }
}
