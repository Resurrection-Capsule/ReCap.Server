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

    public List<CreatureModel> getCreaturesByAccount(AccountModel account) {
        return creatureRepository.getCreaturesByAccountId(account.Id);
    }

    public List<Creature> addAllCreatures(Account account) {
        List<Creature> creatures = [];
        var allTemplates = creatureTemplateRepository.getAllTemplates();
        foreach (var template in allTemplates) {
            creatures.Add(addCreature(account, template));
        }

        account.creatureRewards = allTemplates.Count;
        return creatures;
    }

    public Creature addCreature(Account account, CreatureTemplateModel creatureTemplate) {
        var creature = new Creature{
            Version = 1,
            AccountID = account.Id,
            TemplateID = creatureTemplate.id,
            TemplateName = creatureTemplate.name,
            GearScore = 0,
            ItemPoints = 300
        };
        creatureRepository.insertCreature(creature);
        return creature;
    }
}