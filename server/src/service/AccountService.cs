using HttpServer;

namespace HttpServer;

public class AccountService
{
    private AccountRepositoryAdapter accountRepository;

    public AccountService(SqliteConfig newSqliteConfig) {
        accountRepository = new AccountRepositoryAdapter(newSqliteConfig);
    }

    public Account createAccount(string email, string name, string password, int avatarId) {
        var oldAccount = accountRepository.getAccountByEmail(email);
        if (oldAccount != null) {
            throw new Exception("This e-mail already belongs to a different account");
        }
        var newAccount = new Account{
            Id = "newId",
            Email = email,
            Username = name,
            Password = password
        };
        newAccount.avatarId = avatarId;
        accountRepository.saveAccount(newAccount);
        return newAccount;
    }
}