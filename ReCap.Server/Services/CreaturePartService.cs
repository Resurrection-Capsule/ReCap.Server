using ReCap.Server.Adapters.Persistence.SQLite;
using ReCap.Server.Config;
using ReCap.Server.Domain;
using ReCap.Server.Mappers;
using ReCap.Server.Models;

namespace ReCap.Server.Services;

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

    public CreaturePartModel getCreaturePartById(ulong id) {
        return creaturePartRepository.getCreaturePartById(id);
    }

    public List<CreaturePartModel> getCreaturePartsByAccount(AccountModel account) {
        return creaturePartRepository.getCreaturePartsByAccountId(account.Id);
    }

    public List<CreaturePartModel> addAllCreatureParts(AccountModel account) {
        List<CreaturePartModel> parts = [];
        var allTemplates = creaturePartTemplateRepository.getAllTemplates();
        foreach (var template in allTemplates) {
            var part1 = creaturePartMapper.toCreaturePartModel(template, false);
            part1.AccountId = account.Id;
            parts.Add(part1);

            var part2 = creaturePartMapper.toCreaturePartModel(template, true);
            part2.AccountId = account.Id;
            parts.Add(part2);
        }
        creaturePartRepository.insertCreatureParts(parts);
        return parts;
    }

    public CreaturePartModel addCreaturePart(Account account, CreaturePartTemplateModel creaturePartTemplate) {
        var part = creaturePartMapper.toCreaturePartModel(creaturePartTemplate, false);
        part.AccountId = account.Id;
        creaturePartRepository.insertCreaturePart(part);
        return part;
    }

    public void updateCreaturePart(CreaturePartModel creaturePartModel) {
        creaturePartRepository.updateCreaturePart(creaturePartModel);
    }

    public void updateCreatureParts(List<CreaturePartModel> creaturePartModels) {
        creaturePartRepository.updateCreatureParts(creaturePartModels);
    }

    public void deleteCreaturePart(CreaturePartModel creaturePartModel) {
        creaturePartRepository.deleteCreaturePart(creaturePartModel);
    }
}