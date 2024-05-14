using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace HttpServer;

public class SqliteConfig : DbContext
{
    public DbSet<AccountModel> Accounts { get; set; }

    private string DbPath;

    public SqliteConfig()
    {
        DbPath = ServerConfig.GetServerDatabasePath();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");

    public void Start()
    {
        this.Database.EnsureCreated();
    }
}