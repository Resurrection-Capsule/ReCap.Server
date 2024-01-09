using HttpServer;

namespace HttpServer;

public class AccountService
{
    private AccountRepositoryAdapter accountRepository;

    public AccountService(SqliteConfig newSqliteConfig) {
        accountRepository = new AccountRepositoryAdapter(newSqliteConfig);
    }

    public Account getAccountByAuthToken(string authToken) {
        return accountRepository.getAccountByAuthToken(authToken);
    }

    public Account createAccount(string email, string name, string password, int avatarId) {
        var oldAccount = accountRepository.getAccountByEmail(email);
        if (oldAccount != null) {
            throw new ForbiddenOperationException("This e-mail already belongs to a different account");
        }
        var newAccount = new Account{
            Email = email,
            Username = name,
            Password = password
        };
        newAccount.avatarId = avatarId;
        accountRepository.saveAccount(newAccount);
        return newAccount;
    }
}