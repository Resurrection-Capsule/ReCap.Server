using HttpServer;

namespace HttpServer;

public class PartService
{
    private PartRepositoryAdapter partRepository;

    public PartService(SqliteConfig newSqliteConfig) {
        partRepository = new PartRepositoryAdapter(newSqliteConfig);
    }

    public List<PartModel> getPartsByAccount(AccountModel account) {
        return partRepository.getPartsByAccountId(account.Id);
    }
}