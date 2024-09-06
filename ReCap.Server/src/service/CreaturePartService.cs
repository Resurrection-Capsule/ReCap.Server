using HttpServer;

namespace HttpServer;

public class CreaturePartService
{
    private CreaturePartMapper creaturePartMapper;
    private CreaturePartRepositoryAdapter creaturePartRepository;
    private CreaturePartTemplateRepositoryAdapter creaturePartTemplateRepository;

    public CreaturePartService(SqliteConfig newSqliteConfig) {
        creaturePartMapper = new CreaturePartMapper();
        creaturePartRepository = new CreaturePartRepositoryAdapter(newSqliteConfig);
        creaturePartTemplateRepository = new CreaturePartTemplateRepositoryAdapter(newSqliteConfig);
    }

    public List<CreaturePartModel> getCreaturePartsByAccount(AccountModel account) {
        return creaturePartRepository.getCreaturePartsByAccountId(account.Id);
    }

    public List<CreaturePart> addAllCreatureParts(Account account) {
        List<CreaturePart> parts = [];
        var allTemplates = creaturePartTemplateRepository.getAllTemplates();
        foreach (var template in allTemplates) {
            var part = creaturePartMapper.toDomain(template);
            part.AccountId = account.Id;
            parts.Add(part);
        }
        creaturePartRepository.insertCreatureParts(parts);
        return parts;
    }

    public CreaturePart addCreaturePart(Account account, CreaturePartTemplateModel creaturePartTemplate) {
        var part = creaturePartMapper.toDomain(creaturePartTemplate);
        part.AccountId = account.Id;
        creaturePartRepository.insertCreaturePart(part);
        return part;
    }
}