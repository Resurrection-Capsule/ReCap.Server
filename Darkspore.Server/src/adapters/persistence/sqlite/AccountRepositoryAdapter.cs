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

    public AccountModel getAccountByAuthToken(string authToken)
    {
        // TODO: Not implemented yet
        return sqliteConfig.Accounts.First();
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
        account.Id = 1; // TODO: Generate ID dynamically

        var accountModel = accountMapper.toModel(account);
        sqliteConfig.Accounts.Add(accountModel);
        sqliteConfig.SaveChanges();
    }
}
