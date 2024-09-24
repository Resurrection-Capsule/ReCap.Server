using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;
using LoggerUtil;

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
        Logger.info($"Removing auth token {authToken}");
        idByAuthToken.Remove(authToken);
    }

    public void setAccountAuthToken(ulong accountId, string authToken)
    {
        Logger.info($"Setting auth token for account {accountId}: {authToken}");
        idByAuthToken[authToken] = accountId;
    }

    public AccountModel getAccountByAuthToken(string authToken)
    {
        ulong accountId = 0;
        if (idByAuthToken.TryGetValue(authToken, out accountId))
        {
            Logger.info($"Getting auth token for account {accountId}: {authToken}");
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

    public void updateAccount(AccountModel accountModel) {
        sqliteConfig.SaveChanges();
    }
}
