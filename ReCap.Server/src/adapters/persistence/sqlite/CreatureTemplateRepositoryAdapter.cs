namespace ReCap.Server.Adapters.Persistence.SQLite.CreatureTemplateRepository;

using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

using ReCap.Server.Config.Sqlite;
using ReCap.Server.Mapper.CreatureTemplate;
using ReCap.Server.Model.CreatureTemplate;

public class CreatureTemplateRepositoryAdapter
{
    private SqliteConfig sqliteConfig;
    private CreatureTemplateMapper creatureTemplateMapper;

    public CreatureTemplateRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        creatureTemplateMapper = new CreatureTemplateMapper();
    }

    public List<CreatureTemplateModel> getAllTemplates()
    {
        return sqliteConfig.CreatureTemplates.ToList();
    }

    public CreatureTemplateModel getTemplateById(ulong id)
    {
        return sqliteConfig.CreatureTemplates.SingleOrDefault(b => b.id == id);
    }
}
