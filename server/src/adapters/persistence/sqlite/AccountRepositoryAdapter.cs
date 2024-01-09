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

    public Account getAccountByEmail(string email)
    {
        var accountModel = sqliteConfig.Accounts.Where(b => b.Email == email).First();
        return accountMapper.toDomain(accountModel);
    }

    public void saveAccount(Account account)
    {
        Guid myuuid = Guid.NewGuid();
        account.Id = myuuid.ToString();

        var accountModel = accountMapper.toModel(account);
        sqliteConfig.Accounts.Add(accountModel);
        sqliteConfig.SaveChanges();
    }
}
