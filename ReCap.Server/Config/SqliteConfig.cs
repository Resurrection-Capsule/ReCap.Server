using System.Text.Json;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ReCap.Server.Models;

namespace ReCap.Server.Config;

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

    // Test/diagnostic ctor: point the context at an explicit database file.
    public SqliteConfig(string dbPath)
    {
        DbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");

    public void Start()
    {
        this.Database.EnsureCreated();

        string resDir = ServerConfig.ResourcesDirectory;
        if (this.CreatureTemplates.SingleOrDefault(b => b.id == 1667741389) == null) {
            var templatesStr = File.ReadAllText(Path.Combine(resDir, "creature_templates.json"));
            var templates = JsonSerializer.Deserialize<List<CreatureTemplateModel>>(templatesStr)
                ?? throw new InvalidDataException("creature_templates.json: invalid seed data");
            this.CreatureTemplates.AddRange(templates);
            this.SaveChanges();
        }
        if (this.CreaturePartTemplates.SingleOrDefault(b => b.rigblockAssetId == 1) == null) {
            var partsStr = File.ReadAllText(Path.Combine(resDir, "part_templates.json"));
            var parts = JsonSerializer.Deserialize<List<CreaturePartTemplateModel>>(partsStr)
                ?? throw new InvalidDataException("part_templates.json: invalid seed data");
            this.CreaturePartTemplates.AddRange(parts);
            this.SaveChanges();
        }
    }
}