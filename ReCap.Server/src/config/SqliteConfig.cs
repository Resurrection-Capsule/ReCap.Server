namespace ReCap.Server.Config.Sqlite;

using System.Text.Json;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

using ReCap.Server.Config.Server;

using ReCap.Server.Model.Account;
using ReCap.Server.Model.Creature;
using ReCap.Server.Model.CreaturePart;
using ReCap.Server.Model.CreaturePartTemplate;
using ReCap.Server.Model.CreatureTemplate;
using ReCap.Server.Model.DbSequence;
using ReCap.Server.Model.Deck;

public class SqliteConfig : DbContext
{
    public DbSet<DbSequenceModel> DbSequences { get; set; }
    public DbSet<AccountModel> Accounts { get; set; }
    public DbSet<CreatureModel> Creatures { get; set; }
    public DbSet<CreatureTemplateModel> CreatureTemplates { get; set; }
    public DbSet<CreaturePartModel> CreatureParts { get; set; }
    public DbSet<CreaturePartTemplateModel> CreaturePartTemplates { get; set; }
    public DbSet<DeckModel> Decks { get; set; }

    private string DbPath;

    public SqliteConfig()
    {
        DbPath = ServerConfig.ServerDatabasePath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");

    public void Start()
    {
        this.Database.EnsureCreated();

        string resDir = ServerConfig.ResourcesDirectory;
        if (this.CreatureTemplates.SingleOrDefault(b => b.id == 1667741389) == null) {
            var templatesStr = File.ReadAllText(Path.Combine(resDir, "creature_templates.json"));
            var templates = JsonSerializer.Deserialize<List<CreatureTemplateModel>>(templatesStr);
            this.CreatureTemplates.AddRange(templates);
            this.SaveChanges();
        }
        if (this.CreaturePartTemplates.SingleOrDefault(b => b.rigblockAssetId == 1) == null) {
            var partsStr = File.ReadAllText(Path.Combine(resDir, "part_templates.json"));
            var parts = JsonSerializer.Deserialize<List<CreaturePartTemplateModel>>(partsStr);
            this.CreaturePartTemplates.AddRange(parts);
            this.SaveChanges();
        }
    }
}