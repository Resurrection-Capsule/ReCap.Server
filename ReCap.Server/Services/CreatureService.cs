using ReCap.Server.Adapters.Persistence.SQLite;
using ReCap.Server.Config;
using ReCap.Server.Domain;
using ReCap.Server.Models;

namespace ReCap.Server.Services;

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
        // C++ API.cpp:783 leaves creatureRewards = 100 (a flat seed); it does NOT add one per
        // creature. The per-creature ++ inflated it to 100+N (demo-account count drift).
        return creatureModel;
    }

    public void updateCreature(CreatureModel creatureModel) {
        creatureRepository.updateCreature(creatureModel);
    }
}