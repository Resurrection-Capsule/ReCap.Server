using HttpServer;

namespace HttpServer;

public class AccountService
{
    private AccountRepositoryAdapter accountRepository;

    public AccountService(SqliteConfig newSqliteConfig) {
        accountRepository = new AccountRepositoryAdapter(newSqliteConfig);
    }

    public Account getAccountById(ulong id) {
        var account = accountRepository.getAccountById(id);
        if (account == null) {
            throw new ForbiddenOperationException("Account ID not found");
        }
        return account;
    }

    public Account getAccountByEmailAndPassword(string email, string password) {
        var account = accountRepository.getAccountByEmail(email);
        if (account == null) {
            throw new ForbiddenOperationException("This e-mail does not belong to any account");
        }
        if (account.Password != password) {
            throw new ForbiddenOperationException("Invalid password");
        }
        return account;
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