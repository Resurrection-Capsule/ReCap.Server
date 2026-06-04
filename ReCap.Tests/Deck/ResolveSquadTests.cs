using System.Collections.Generic;
using System.Linq;
using ReCap.Server.Adapters.Persistence.SQLite;
using ReCap.Server.Config;
using ReCap.Server.Domain;
using ReCap.Server.Services;
using ReCap.Tests.TestSupport;
using Xunit;

namespace ReCap.Tests.Deck;

public class ResolveSquadTests
{
    [Fact]
    public void Empty_deck_resolves_to_empty_squad_with_no_injection()
    {
        var path = TempDb.NewPath();
        try
        {
            using var db = new SqliteConfig(path);
            db.Database.EnsureCreated();
            var accountService = new AccountService(db);
            var account = accountService.createAccount("t@t.t", "tester", "pw", 1, false);

            // The account OWNS creatures, but the deck is empty — nothing may be injected.
            var creatureRepo = new CreatureRepositoryAdapter(db);
            creatureRepo.insertCreature(new Creature { Version = 1, AccountID = account.Id, TemplateID = 1001, TemplateName = "a" });
            creatureRepo.insertCreature(new Creature { Version = 1, AccountID = account.Id, TemplateID = 1002, TemplateName = "b" });

            var deckService = new DeckService(db);
            deckService.createDecksForAccount(account.Id);

            var gameService = new GameService { Decks = deckService, Creatures = new CreatureService(db) };
            var squad = gameService.ResolveSquad(account, 1);

            Assert.Empty(squad);
        }
        finally { TempDb.TryDelete(path); }
    }

    [Fact]
    public void Squad_is_exactly_the_persisted_deck_slots_in_order()
    {
        var path = TempDb.NewPath();
        try
        {
            using var db = new SqliteConfig(path);
            db.Database.EnsureCreated();
            var accountService = new AccountService(db);
            var account = accountService.createAccount("t@t.t", "tester", "pw", 1, false);

            var creatureRepo = new CreatureRepositoryAdapter(db);
            var c1 = creatureRepo.insertCreature(new Creature { Version = 1, AccountID = account.Id, TemplateID = 1001, TemplateName = "a" });
            var c2 = creatureRepo.insertCreature(new Creature { Version = 1, AccountID = account.Id, TemplateID = 1002, TemplateName = "b" });

            var deckService = new DeckService(db);
            deckService.createDecksForAccount(account.Id);
            deckService.updateDeck(account.Id, 1,
                new List<ulong> { c1.ID, c2.ID },
                new HashSet<ulong> { c1.ID, c2.ID }, "pve");

            var gameService = new GameService { Decks = deckService, Creatures = new CreatureService(db) };
            var squad = gameService.ResolveSquad(account, 1);

            Assert.Equal(2, squad.Count);
            Assert.Equal((uint)1001, squad[0].Noun);
            Assert.Equal((uint)1002, squad[1].Noun);
        }
        finally { TempDb.TryDelete(path); }
    }

    [Fact]
    public void Missing_deck_resolves_to_empty_squad()
    {
        var path = TempDb.NewPath();
        try
        {
            using var db = new SqliteConfig(path);
            db.Database.EnsureCreated();
            var accountService = new AccountService(db);
            var account = accountService.createAccount("t@t.t", "tester", "pw", 1, false);

            var gameService = new GameService
            {
                Decks = new DeckService(db),
                Creatures = new CreatureService(db)
            };
            // No decks created at all -> C++ GetSquadById miss -> silent empty.
            Assert.Empty(gameService.ResolveSquad(account, 1));
        }
        finally { TempDb.TryDelete(path); }
    }
}
