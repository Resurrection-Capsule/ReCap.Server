using System.Text.Json;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace HttpServer;

public class SqliteConfig : DbContext
{
    public DbSet<AccountModel> Accounts { get; set; }
    public DbSet<CreatureModel> Creatures { get; set; }
    public DbSet<CreatureTemplateModel> CreatureTemplates { get; set; }
    public DbSet<CreaturePartTemplateModel> CreaturePartTemplates { get; set; }

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
            var templatesStr = File.ReadAllText("./resources/creature_templates.json");
            var templates = JsonSerializer.Deserialize<List<CreatureTemplateModel>>(templatesStr);
            this.CreatureTemplates.AddRange(templates);
            this.SaveChanges();
        }
        if (this.CreaturePartTemplates.SingleOrDefault(b => b.rigblockAssetId == 1) == null) {
            var partsStr = File.ReadAllText("./resources/part_templates.json");
            var parts = JsonSerializer.Deserialize<List<CreaturePartTemplateModel>>(partsStr);
            this.CreaturePartTemplates.AddRange(parts);
            this.SaveChanges();
        }
    }
}