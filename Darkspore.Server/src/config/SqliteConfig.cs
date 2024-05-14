using System.Text.Json;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace HttpServer;

public class SqliteConfig : DbContext
{
    public DbSet<AccountModel> Accounts { get; set; }
    public DbSet<CreatureTemplateModel> CreatureTemplates { get; set; }
    public DbSet<CreaturePartModel> CreatureParts { get; set; }

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
        if (this.CreatureParts.SingleOrDefault(b => b.rigblockAssetId == 1) == null) {
            var partsStr = File.ReadAllText("./resources/parts.json");
            var parts = JsonSerializer.Deserialize<List<CreaturePartModel>>(partsStr);
            this.CreatureParts.AddRange(parts);
            this.SaveChanges();
        }
    }
}