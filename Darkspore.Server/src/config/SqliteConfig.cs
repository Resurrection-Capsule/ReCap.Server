using System.Text.Json;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace HttpServer;

public class SqliteConfig : DbContext
{
    public DbSet<AccountModel> Accounts { get; set; }
    public DbSet<CreatureTemplateModel> CreatureTemplates { get; set; }

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

        if (this.CreatureTemplates.SingleOrDefault(b => b.id == 1667741389) == null) {
            var templatesStr = File.ReadAllText("./resources/templates.json");
            var templates = JsonSerializer.Deserialize<List<CreatureTemplateModel>>(templatesStr);
            this.CreatureTemplates.AddRange(templates);
            this.SaveChanges();
        }
    }
}