using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer;

public class AccountRepositoryAdapter
{
    private SqliteConfig sqliteConfig;
    private AccountMapper accountMapper;

    public AccountRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        accountMapper = new AccountMapper();
    }

    public Account getAccountByAuthToken(string authToken)
    {
        // TODO: Not implemented yet
        var accountModel = sqliteConfig.Accounts.First();
        return accountMapper.toDomain(accountModel);
    }

    public Account getAccountById(ulong id)
    {
        var accountModel = sqliteConfig.Accounts.SingleOrDefault(b => b.Id == id);
        if (accountModel == null) {
            return null;
        }
        return accountMapper.toDomain(accountModel);
    }

    public Account getAccountByEmail(string email)
    {
        var accountModel = sqliteConfig.Accounts.SingleOrDefault(b => b.Email == email);
        if (accountModel == null) {
            return null;
        }
        return accountMapper.toDomain(accountModel);
    }

    public void insertAccount(Account account)
    {
        account.Id = 1; // TODO: Generate ID dynamically

        var accountModel = accountMapper.toModel(account);
        sqliteConfig.Accounts.Add(accountModel);
        sqliteConfig.SaveChanges();
    }
}
