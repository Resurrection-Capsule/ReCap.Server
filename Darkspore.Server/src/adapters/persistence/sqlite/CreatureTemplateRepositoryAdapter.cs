using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer;

public class CreatureTemplateRepositoryAdapter
{
    private SqliteConfig sqliteConfig;
    private CreatureTemplateMapper creatureTemplateMapper;

    public CreatureTemplateRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        creatureTemplateMapper = new CreatureTemplateMapper();
    }

    public List<CreatureTemplate> getAllTemplates()
    {
        return sqliteConfig.CreatureTemplates.ToList()
            .Select(creatureTemplate => creatureTemplateMapper.toDomain(creatureTemplate)).ToList();
    }

    public CreatureTemplate getTemplateById(ulong id)
    {
        var creatureTemplateModel = sqliteConfig.CreatureTemplates.SingleOrDefault(b => b.id == id);
        if (creatureTemplateModel == null) {
            return null;
        }
        return creatureTemplateMapper.toDomain(creatureTemplateModel);
    }
}
