using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer;

public class AccountRepositoryAdapter
{
    private static string ACCOUNT_SEQUENCE_NAME = "ACCOUNT_SEQUENCE";

    private SqliteConfig sqliteConfig;
    private AccountMapper accountMapper;
    private DbSequenceAdapter sequenceRandomGenerator;

    private static Dictionary<string,ulong> idByAuthToken = new Dictionary<string,ulong>();

    public AccountRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        accountMapper = new AccountMapper();
        sequenceRandomGenerator = new DbSequenceAdapter(newSqliteConfig);
    }

    public void deleteAuthToken(string authToken)
    {
        Console.WriteLine($"Removing auth token {authToken}");
        idByAuthToken.Remove(authToken);
    }

    public void setAccountAuthToken(ulong accountId, string authToken)
    {
        Console.WriteLine($"Setting auth token for account {accountId}: {authToken}");
        idByAuthToken[authToken] = accountId;
    }

    public AccountModel getAccountByAuthToken(string authToken)
    {
        ulong accountId = 0;
        if (idByAuthToken.TryGetValue(authToken, out accountId))
        {
            Console.WriteLine($"Getting auth token for account {accountId}: {authToken}");
            return getAccountById(accountId);
        }

        return null;
    }

    public AccountModel getAccountById(ulong id)
    {
        return sqliteConfig.Accounts.SingleOrDefault(b => b.Id == id);
    }

    public AccountModel getAccountByEmail(string email)
    {
        return sqliteConfig.Accounts.SingleOrDefault(b => b.Email == email);
    }

    public void insertAccount(Account account)
    {
        account.Id = (ulong)sequenceRandomGenerator.Next(ACCOUNT_SEQUENCE_NAME);

        var accountModel = accountMapper.toModel(account);
        sqliteConfig.Accounts.Add(accountModel);
        sqliteConfig.SaveChanges();
    }
}
