using System.Collections.Generic;
using System.Linq;
using ReCap.Server.Config;
using ReCap.Server.Services;
using ReCap.Tests.TestSupport;
using Xunit;

namespace ReCap.Tests.Deck;

public class DeckServiceTests
{
    [Fact]
    public void Virgin_account_gets_three_empty_unlocked_decks()
    {
        var path = TempDb.NewPath();
        try
        {
            using var db = new SqliteConfig(path);
            db.Database.EnsureCreated();
            var decks = new DeckService(db).createDecksForAccount(accountId: 42);

            Assert.Equal(3, decks.Count);
            Assert.Equal(new[] { 1, 2, 3 }, decks.Select(d => d.Slot).ToArray());
            Assert.All(decks, d =>
            {
                Assert.Equal(new List<ulong> { 0, 0, 0 }, d.CreatureIds);
                Assert.False(d.Locked);
                Assert.Equal("pve", d.Category);
                Assert.Equal($"Slot {d.Slot}", d.Name);
            });
        }
        finally { TempDb.TryDelete(path); }
    }

    [Fact]
    public void UpdateDeck_writes_validated_slots_and_category_to_the_active_deck_only()
    {
        var path = TempDb.NewPath();
        try
        {
            using var db = new SqliteConfig(path);
            db.Database.EnsureCreated();
            var service = new DeckService(db);
            service.createDecksForAccount(accountId: 42);

            var owned = new HashSet<ulong> { 442, 475, 416 };
            // Client wire shape: all 9 slots, zeros for empty, plus a foreign id.
            service.updateDeck(42, slot: 1,
                requestedIds: new List<ulong> { 442, 999, 475, 0, 0, 0, 0, 0, 0 },
                ownedCreatureIds: owned,
                category: "pve");

            var decks = service.getDecksByAccountId(42);
            var deck1 = decks.Single(d => d.Slot == 1);
            Assert.Equal(new List<ulong> { 442, 475, 0 }, deck1.CreatureIds);
            Assert.Equal("pve", deck1.Category);

            // Other decks untouched.
            Assert.Equal(new List<ulong> { 0, 0, 0 }, decks.Single(d => d.Slot == 2).CreatureIds);
            Assert.Equal(new List<ulong> { 0, 0, 0 }, decks.Single(d => d.Slot == 3).CreatureIds);
        }
        finally { TempDb.TryDelete(path); }
    }

    [Fact]
    public void UpdateDeck_with_unknown_slot_is_a_noop()
    {
        var path = TempDb.NewPath();
        try
        {
            using var db = new SqliteConfig(path);
            db.Database.EnsureCreated();
            var service = new DeckService(db);
            service.createDecksForAccount(accountId: 42);

            // The client sometimes sends a garbage pvp_active_slot (seen in logs: 28224632).
            service.updateDeck(42, slot: 28224632,
                requestedIds: new List<ulong> { 442 },
                ownedCreatureIds: new HashSet<ulong> { 442 },
                category: "pvp");

            Assert.All(service.getDecksByAccountId(42),
                d => Assert.Equal(new List<ulong> { 0, 0, 0 }, d.CreatureIds));
        }
        finally { TempDb.TryDelete(path); }
    }
}
