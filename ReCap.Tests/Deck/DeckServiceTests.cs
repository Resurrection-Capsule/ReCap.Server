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
}
