using System;
using System.Collections.Generic;
using System.Linq;
using ReCap.Server.Config;
using ReCap.Server.Models;
using ReCap.Tests.TestSupport;
using Xunit;

namespace ReCap.Tests.Persistence;

public class DeckPersistenceTests
{
    [Fact]
    public void CreatureIds_survive_update_and_fresh_context_read()
    {
        var path = TempDb.NewPath();
        try
        {
            using (var db = new SqliteConfig(path))
            {
                db.Database.EnsureCreated();
                db.Decks.Add(new DeckModel
                {
                    ID = 1, Name = "Slot 1", Slot = 1, Category = "pve",
                    AccountID = 42, Locked = false,
                    CreatureIds = new List<ulong> { 1, 2, 3 }
                });
                db.SaveChanges();
            }

            // The real updateDeck path: reference-assign a new list, Update, SaveChanges.
            using (var db = new SqliteConfig(path))
            {
                var deck = db.Decks.Single(d => d.ID == 1);
                deck.CreatureIds = new List<ulong> { 4, 5, 0 };
                db.Decks.Update(deck);
                db.SaveChanges();
            }

            // Server-restart equivalent: a brand-new context must read the edit, not the seed.
            using (var db = new SqliteConfig(path))
            {
                var deck = db.Decks.Single(d => d.ID == 1);
                Assert.Equal(new List<ulong> { 4, 5, 0 }, deck.CreatureIds);
            }
        }
        finally { TempDb.TryDelete(path); }
    }

    [Fact]
    public void Empty_slots_round_trip()
    {
        var path = TempDb.NewPath();
        try
        {
            using (var db = new SqliteConfig(path))
            {
                db.Database.EnsureCreated();
                db.Decks.Add(new DeckModel
                {
                    ID = 2, Name = "Slot 2", Slot = 2, Category = "pve",
                    AccountID = 42, Locked = false,
                    CreatureIds = new List<ulong> { 0, 0, 0 }
                });
                db.SaveChanges();
            }
            using (var db = new SqliteConfig(path))
            {
                var deck = db.Decks.Single(d => d.ID == 2);
                Assert.Equal(new List<ulong> { 0, 0, 0 }, deck.CreatureIds);
            }
        }
        finally { TempDb.TryDelete(path); }
    }
}
