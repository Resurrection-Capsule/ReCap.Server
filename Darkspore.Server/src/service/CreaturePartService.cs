using HttpServer;

namespace HttpServer;

public class CreaturePartService
{
    private CreaturePartRepositoryAdapter creaturePartRepository;

    public CreaturePartService(SqliteConfig newSqliteConfig) {
        creaturePartRepository = new CreaturePartRepositoryAdapter(newSqliteConfig);
    }

    public List<CreaturePartModel> getCreaturePartsByAccount(AccountModel account) {
        return creaturePartRepository.getCreaturePartsByAccountId(account.Id);
    }
}