using HttpServer;

namespace HttpServer;

public class CreatureService
{
    private CreatureRepositoryAdapter creatureRepository;
    private CreatureTemplateRepositoryAdapter creatureTemplateRepository;

    public CreatureService(SqliteConfig newSqliteConfig) {
        creatureRepository = new CreatureRepositoryAdapter(newSqliteConfig);
        creatureTemplateRepository = new CreatureTemplateRepositoryAdapter(newSqliteConfig);
    }

    public CreatureTemplateModel getCreatureTemplateById(ulong templateId) {
        return creatureTemplateRepository.getTemplateById(templateId);
    }

    public CreatureModel getCreatureById(ulong creatureId) {
        return creatureRepository.getCreatureById(creatureId);
    }

    public List<CreatureModel> getCreaturesByAccount(AccountModel account) {
        return creatureRepository.getCreaturesByAccountId(account.Id);
    }

    public List<CreatureModel> addAllCreatures(AccountModel account) {
        List<CreatureModel> creatures = [];
        var allTemplates = creatureTemplateRepository.getAllTemplates();
        foreach (var template in allTemplates) {
            creatures.Add(addCreature(account, template));
        }
        return creatures;
    }

    public CreatureModel addCreature(AccountModel account, CreatureTemplateModel creatureTemplate) {
        var creature = new Creature{
            Version = 1,
            AccountID = account.Id,
            TemplateID = creatureTemplate.id,
            TemplateName = creatureTemplate.name,
            GearScore = 0,
            ItemPoints = 300
        };
        var creatureModel = creatureRepository.insertCreature(creature);
        account.creatureRewards++;
        return creatureModel;
    }

    public void updateCreature(CreatureModel creatureModel) {
        creatureRepository.updateCreature(creatureModel);
    }
}