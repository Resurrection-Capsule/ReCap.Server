# Deck/Squad System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Player-driven deck system — 3 empty positional slots per deck, ownership-validated updates, no runtime creature injection, verified SQLite persistence round-trip.

**Architecture:** Mirror the C++ contract (spec: `docs/superpowers/specs/2026-06-04-deck-system-design.md`): decks are created empty (`[0,0,0]`), `updateDecks` writes only the active slot's deck with ownership-validated compacted ids, `ResolveSquad` serves exactly the persisted deck (fallback deleted), and `CreatureIds` persistence is locked by a round-trip test.

**Tech Stack:** .NET (ReCap.Server, EF Core 9 + SQLite), xUnit (ReCap.Tests).

**Build/test notes:**
- The server exe locks `bin/.../ReCap.Server.exe` — **stop any running ReCap.Server before building/testing** (else MSB3027 file-lock errors, not code errors).
- Run all tests: `dotnet test ReCap.Tests/ReCap.Tests.csproj -v q`
- Commits in this repo carry **no Co-Authored-By line** (user preference).
- Working dir for all commands: `C:\CodingProjects\Personal\ReCap`.

---

### Task 1: Persistence round-trip (the linchpin)

`DeckModel.CreatureIds` is `List<ulong>` with no explicit EF mapping. EF Core 9 should map primitive collections to a JSON column natively — this task PROVES it (and ships the fix if it doesn't). The real update path assigns a NEW list instance (reference swap), so that is what the test exercises, plus a server-restart-equivalent fresh-context read.

**Files:**
- Modify: `ReCap.Server/Config/SqliteConfig.cs` (add path ctor)
- Create: `ReCap.Tests/TestSupport/TempDb.cs`
- Create: `ReCap.Tests/Persistence/DeckPersistenceTests.cs`

- [ ] **Step 1: Add a test-pathable ctor to SqliteConfig**

In `ReCap.Server/Config/SqliteConfig.cs`, after the existing parameterless ctor (lines 20-23), add:

```csharp
    // Test/diagnostic ctor: point the context at an explicit database file.
    public SqliteConfig(string dbPath)
    {
        DbPath = dbPath;
    }
```

- [ ] **Step 2: Create the temp-DB test helper**

Create `ReCap.Tests/TestSupport/TempDb.cs`:

```csharp
using System;
using System.IO;

namespace ReCap.Tests.TestSupport;

internal static class TempDb
{
    public static string NewPath() =>
        Path.Combine(Path.GetTempPath(), $"recap-test-{Guid.NewGuid():N}.db");

    public static void TryDelete(string path)
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
        catch { /* temp dir, best effort */ }
    }
}
```

- [ ] **Step 3: Write the failing/characterization round-trip test**

Create `ReCap.Tests/Persistence/DeckPersistenceTests.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~DeckPersistenceTests" -v q`

Expected: **PASS** (EF Core 9 maps `List<ulong>` to a JSON column natively). If both pass, skip Step 5.

- [ ] **Step 5 (ONLY if Step 4 fails): add explicit conversion + comparer**

In `ReCap.Server/Config/SqliteConfig.cs`, add usings and override:

```csharp
using Microsoft.EntityFrameworkCore.ChangeTracking;
```

```csharp
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // List<ulong> <-> CSV column with a comparer so EF detects list changes.
        modelBuilder.Entity<DeckModel>()
            .Property(d => d.CreatureIds)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(ulong.Parse).ToList(),
                new ValueComparer<List<ulong>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x.GetHashCode())),
                    v => v.ToList()));
    }
```

Re-run Step 4. Expected: PASS. (Note: changing the storage format obsoletes existing dev `server.db` files — they are disposable, see Task 7 manual gate.)

- [ ] **Step 6: Commit**

```powershell
git add ReCap.Server/Config/SqliteConfig.cs ReCap.Tests/TestSupport/TempDb.cs ReCap.Tests/Persistence/DeckPersistenceTests.cs
git commit -m "test(deck): lock CreatureIds SQLite round-trip; SqliteConfig test ctor"
```

---

### Task 2: BuildSlots — ownership validation + compaction (pure logic)

Mirror of C++ `User::UpdateSquad` (User.cpp:260-289): skip zeros, skip ids the account does not own, compact valid ids into 3 positional slots, zero-fill the rest. The client sends `pve_creatures` as 9 ids (3 decks x 3 slots, zeros for empty) — only valid ids survive.

**Files:**
- Modify: `ReCap.Server/Services/DeckService.cs`
- Create: `ReCap.Tests/Deck/DeckSlotsTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `ReCap.Tests/Deck/DeckSlotsTests.cs`:

```csharp
using System.Collections.Generic;
using ReCap.Server.Services;
using Xunit;

namespace ReCap.Tests.Deck;

public class DeckSlotsTests
{
    private static readonly HashSet<ulong> Owned = new() { 442, 475, 416 };

    [Fact]
    public void Client_nine_id_csv_compacts_to_three_slots()
    {
        // Real wire shape seen in logs: "442,475,416,0,0,0,0,0,0"
        var result = DeckService.BuildSlots(
            new List<ulong> { 442, 475, 416, 0, 0, 0, 0, 0, 0 }, Owned);
        Assert.Equal(new List<ulong> { 442, 475, 416 }, result);
    }

    [Fact]
    public void Foreign_ids_are_skipped()
    {
        var result = DeckService.BuildSlots(
            new List<ulong> { 442, 999, 416 }, Owned);
        Assert.Equal(new List<ulong> { 442, 416, 0 }, result);
    }

    [Fact]
    public void Interleaved_zeros_compact()
    {
        var result = DeckService.BuildSlots(
            new List<ulong> { 0, 442, 0, 475 }, Owned);
        Assert.Equal(new List<ulong> { 442, 475, 0 }, result);
    }

    [Fact]
    public void More_than_three_valid_caps_at_three()
    {
        var owned = new HashSet<ulong> { 1, 2, 3, 4 };
        var result = DeckService.BuildSlots(new List<ulong> { 1, 2, 3, 4 }, owned);
        Assert.Equal(new List<ulong> { 1, 2, 3 }, result);
    }

    [Fact]
    public void Nothing_valid_yields_empty_deck()
    {
        var result = DeckService.BuildSlots(new List<ulong> { 0, 999 }, Owned);
        Assert.Equal(new List<ulong> { 0, 0, 0 }, result);
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~DeckSlotsTests" -v q`
Expected: FAIL — `'DeckService' does not contain a definition for 'BuildSlots'` (compile error counts as the failing state).

- [ ] **Step 3: Implement BuildSlots**

In `ReCap.Server/Services/DeckService.cs`, add to the class:

```csharp
    // Mirrors C++ User::UpdateSquad (User.cpp:260-289): each non-zero id must belong to
    // the account; valid ids are compacted into the 3 positional slots (index++), the
    // remainder stays 0. A deck always holds exactly 3 positional values (Squad.h:34).
    public static List<ulong> BuildSlots(IEnumerable<ulong> requestedIds, IReadOnlySet<ulong> ownedCreatureIds)
    {
        var slots = new List<ulong> { 0, 0, 0 };
        int index = 0;
        foreach (var id in requestedIds)
        {
            if (index >= slots.Count) break;
            if (id == 0 || !ownedCreatureIds.Contains(id)) continue;
            slots[index++] = id;
        }
        return slots;
    }
```

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~DeckSlotsTests" -v q`
Expected: 5 PASS.

- [ ] **Step 5: Commit**

```powershell
git add ReCap.Server/Services/DeckService.cs ReCap.Tests/Deck/DeckSlotsTests.cs
git commit -m "feat(deck): BuildSlots — ownership-validated compaction into 3 positional slots"
```

---

### Task 3: createDecksForAccount — empty decks

Mirror of C++ `ResetSquads` (User.cpp:247-258): 3 unlocked decks "Slot 1..3", all slots empty. The C++ SignUp slot-0 creature fill (API.cpp:808-814) is a `TODO: remove` test hack — deliberately NOT ported (user decision: decks are player-built).

**Files:**
- Modify: `ReCap.Server/Services/DeckService.cs` (replace `createDecksForAccount`)
- Modify: `ReCap.Server/Adapters/Rest/ReCapRestController.cs:53-55` (call site)
- Create: `ReCap.Tests/Deck/DeckServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `ReCap.Tests/Deck/DeckServiceTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~DeckServiceTests" -v q`
Expected: FAIL — `createDecksForAccount` signature mismatch (current one takes `(AccountModel, List<CreatureModel>)`).

- [ ] **Step 3: Replace createDecksForAccount**

In `ReCap.Server/Services/DeckService.cs`, replace the whole existing `createDecksForAccount(AccountModel account, List<CreatureModel> creatures)` method with:

```csharp
    // Mirrors C++ ResetSquads (User.cpp:247-258): 3 unlocked squads "Slot 1..3" with all
    // creature slots EMPTY ({0,0,0}). The SignUp slot-0 creature fill (API.cpp:808-814)
    // is a "TODO: remove" test hack in C++ and is deliberately not ported — decks are
    // player-built (spec 2026-06-04-deck-system-design).
    public List<Deck> createDecksForAccount(ulong accountId)
    {
        List<Deck> decks = [];
        for (int squadSlot = 1; squadSlot <= 3; squadSlot++)
        {
            var deck = new Deck
            {
                Name = "Slot " + squadSlot,
                Slot = squadSlot,
                Category = "pve",
                AccountID = accountId,
                Locked = false,
                CreatureIds = [0, 0, 0]
            };
            deckRepository.insertDeck(deck);
            decks.Add(deck);
        }
        return decks;
    }
```

- [ ] **Step 4: Fix the call site**

In `ReCap.Server/Adapters/Rest/ReCapRestController.cs` (lines 53-55), replace:

```csharp
            var creatures = creatureService.addAllCreatures(account);
            deckService.createDecksForAccount(account, creatures);
            creaturePartService.addAllCreatureParts(account);
```

with:

```csharp
            creatureService.addAllCreatures(account);
            deckService.createDecksForAccount(account.Id);
            creaturePartService.addAllCreatureParts(account);
```

- [ ] **Step 5: Run the test + full build**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~DeckServiceTests" -v q`
Expected: PASS (and the solution compiles — the old signature has no other callers).

- [ ] **Step 6: Commit**

```powershell
git add ReCap.Server/Services/DeckService.cs ReCap.Server/Adapters/Rest/ReCapRestController.cs ReCap.Tests/Deck/DeckServiceTests.cs
git commit -m "feat(deck): virgin accounts get 3 empty decks (C++ ResetSquads; seeding hack not ported)"
```

---

### Task 4: updateDeck — validated, active-deck-only write

Mirror of C++ `game_deck_updateDecks` + `User::UpdateSquad` (API.cpp:1866-1881, User.cpp:260-289): only the deck whose `Slot == active_slot` is touched; ids are validated against the account's creatures; category follows the pve/pvp channel.

**Files:**
- Modify: `ReCap.Server/Services/DeckService.cs` (replace `updateDeck`)
- Modify: `ReCap.Server/Adapters/Rest/GameRestController.cs:395-417` (`updateDecks` + `ApplyDeckUpdate`)
- Test: `ReCap.Tests/Deck/DeckServiceTests.cs` (extend)

- [ ] **Step 1: Write the failing tests**

Append to `ReCap.Tests/Deck/DeckServiceTests.cs` (inside the class):

```csharp
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
```

Note: these tests use `getDecksByAccountId(ulong)` — added in Step 3 alongside the new signature.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~DeckServiceTests" -v q`
Expected: FAIL (compile — new `updateDeck`/`getDecksByAccountId` signatures don't exist yet).

- [ ] **Step 3: Implement the new updateDeck (+ id-based getter)**

In `ReCap.Server/Services/DeckService.cs`, replace the existing `updateDeck(AccountModel account, int slot, List<ulong> creatureIds)` with:

```csharp
    public List<DeckModel> getDecksByAccountId(ulong accountId)
    {
        return deckRepository.getDecksByAccountId(accountId);
    }

    // Persist a squad/deck edit (api.deck.updateDecks). Mirrors C++ User::UpdateSquad
    // (User.cpp:260-289): only the active slot's deck is touched; every id must belong
    // to the account; valid ids are compacted, the deck always stores 3 positional
    // values; category follows the pve/pvp channel. Unknown slot = silent no-op
    // (C++ GetSquadById miss).
    public void updateDeck(ulong accountId, int slot, List<ulong> requestedIds,
                           IReadOnlySet<ulong> ownedCreatureIds, string category)
    {
        var deck = deckRepository.getDecksByAccountId(accountId).FirstOrDefault(d => d.Slot == slot);
        if (deck == null) return;
        deck.CreatureIds = BuildSlots(requestedIds, ownedCreatureIds);
        deck.Category = category;
        deckRepository.updateDeck(deck);
    }
```

Keep the existing `getDecksByAccount(AccountModel account)` (other callers use it).

- [ ] **Step 4: Rewire the REST handler**

In `ReCap.Server/Adapters/Rest/GameRestController.cs`, replace `updateDecks` + `ApplyDeckUpdate` (lines 395-417) with:

```csharp
    [RequestMapping(Name="api.deck.updateDecks")]
    public byte[] updateDecks(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        // C++ game_deck_updateDecks (API.cpp:1866-1881): pve_active_slot + pve_creatures
        // (CSV; the client sends all 9 slot values, zeros for empty), idem pvp.
        var account = accountService.getAccountByAuthToken(parameters["token"]);
        var ownedIds = creatureService.getCreaturesByAccount(account).Select(c => c.ID).ToHashSet();

        ApplyDeckUpdate(account.Id, ownedIds, parameters, "pve_active_slot", "pve_creatures", "pve");
        ApplyDeckUpdate(account.Id, ownedIds, parameters, "pvp_active_slot", "pvp_creatures", "pvp");

        return XmlHelper.Serialize(new Contracts.ResponseContract { Stat = "ok", Code = 200, Result = 1 });
    }

    private void ApplyDeckUpdate(ulong accountId, IReadOnlySet<ulong> ownedIds,
                                 Dictionary<string,string> parameters,
                                 string slotKey, string creaturesKey, string category)
    {
        if (!parameters.TryGetValue(slotKey, out var slotStr) || !int.TryParse(slotStr, out var slot))
            return;

        var requestedIds = parameters.GetValueOrDefault(creaturesKey, "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => (ulong)Convert.ToInt64(s.Trim()))
            .ToList();

        deckService.updateDeck(accountId, slot, requestedIds, ownedIds, category);
    }
```

- [ ] **Step 5: Run tests + build**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~DeckServiceTests" -v q`
Expected: all DeckServiceTests PASS, solution compiles.

- [ ] **Step 6: Commit**

```powershell
git add ReCap.Server/Services/DeckService.cs ReCap.Server/Adapters/Rest/GameRestController.cs ReCap.Tests/Deck/DeckServiceTests.cs
git commit -m "feat(deck): ownership-validated updateDecks targeting only the active slot (C++ UpdateSquad)"
```

---

### Task 5: ResolveSquad — serve exactly the persisted deck (fallback deleted)

Mirror of C++ `PrepareGameStart` + `Player::SetSquad` (Server.cpp:1210-1241, Player.cpp:267-313): missing deck → empty squad (silent drop); empty slots → skipped (the hello-LPU `FillSquadCharacters` already pads to 3 with noun=0, mirroring C++ null characters). The "first 3 owned creatures" fallback is the runtime injection being removed.

**Files:**
- Modify: `ReCap.Server/Services/GameService.cs:23-40` (`ResolveSquad`)
- Create: `ReCap.Tests/Deck/ResolveSquadTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `ReCap.Tests/Deck/ResolveSquadTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run to verify the injection test fails**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~ResolveSquadTests" -v q`
Expected: `Empty_deck_resolves_to_empty_squad_with_no_injection` FAILS (current fallback injects the 2 owned creatures). The other two may pass/fail incidentally.

- [ ] **Step 3: Replace ResolveSquad**

In `ReCap.Server/Services/GameService.cs`, replace the `ResolveSquad` method (lines 19-40, including its comment) with:

```csharp
    // Mirrors C++ PrepareGameStart + Player::SetSquad (Server.cpp:1210-1241,
    // Player.cpp:267-313): the squad is EXACTLY the persisted deck. Missing deck or
    // empty slots resolve to an empty/partial squad — the server never injects
    // creatures (spec 2026-06-04-deck-system-design). Empty slots become noun=0
    // characters downstream in FillSquadCharacters, matching C++ null characters.
    public IReadOnlyList<SquadCreature> ResolveSquad(AccountModel account, int squadId)
    {
        if (Decks is null || Creatures is null)
            return Array.Empty<SquadCreature>();

        var deck = Decks.getDecksByAccount(account).FirstOrDefault(d => d.Slot == squadId);
        if (deck is null)
            return Array.Empty<SquadCreature>();

        return deck.CreatureIds
            .Where(id => id != 0)
            .Select(id => Creatures.getCreatureById(id))
            .Where(c => c is not null)
            .Select(ToSquadCreature)
            .ToList();
    }
```

- [ ] **Step 4: Run to verify all pass**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~ResolveSquadTests" -v q`
Expected: 3 PASS.

- [ ] **Step 5: Commit**

```powershell
git add ReCap.Server/Services/GameService.cs ReCap.Tests/Deck/ResolveSquadTests.cs
git commit -m "feat(deck): ResolveSquad serves exactly the persisted deck — injection fallback deleted"
```

---

### Task 6: Serve parity — zero slots never reach the client

`DeckMapper.toContract` already skips unresolvable ids (zero slots find no creature). This test locks that behavior so the C++ "zero-id slots silently skipped" contract (User.cpp:589-620) can't regress.

**Files:**
- Create: `ReCap.Tests/Deck/DeckMapperTests.cs`

- [ ] **Step 1: Write the test**

Create `ReCap.Tests/Deck/DeckMapperTests.cs`:

```csharp
using System.Collections.Generic;
using ReCap.Server.Mappers;
using ReCap.Server.Models;
using Xunit;

namespace ReCap.Tests.Deck;

public class DeckMapperTests
{
    [Fact]
    public void Zero_slots_are_omitted_from_the_contract()
    {
        // C++ WriteSquadsAPI (User.cpp:589-620): zero-id slots are silently skipped.
        var deck = new DeckModel
        {
            ID = 1, Name = "Slot 1", Slot = 1, Category = "pve",
            AccountID = 42, Locked = false,
            CreatureIds = new List<ulong> { 5, 0, 0 }
        };
        var creatures = new List<CreatureModel>
        {
            new CreatureModel { ID = 5, Version = 1, TemplateID = 1001, TemplateName = "a" }
        };

        var contract = new DeckMapper().toContract(deck, creatures);

        Assert.Single(contract.Creatures);
        Assert.Equal(1, contract.Slot);
    }
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~DeckMapperTests" -v q`
Expected: PASS immediately (characterization). If it fails, the mapper regressed — fix there, not in the test.

- [ ] **Step 3: Commit**

```powershell
git add ReCap.Tests/Deck/DeckMapperTests.cs
git commit -m "test(deck): lock zero-slot omission in deck serve (C++ WriteSquadsAPI parity)"
```

---

### Task 7: Full suite, docs, manual gate

**Files:**
- Modify: `docs/architecture/planning/DIVERGENCE_LEDGER.md` (new row)
- Modify: `docs/architecture/deck-system/DECK_SQUAD_SYSTEM.md` (header pointer)

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj -v q`
Expected: all tests pass (37 pre-existing + the new deck tests). Server must not be running.

- [ ] **Step 2: Ledger row**

In `docs/architecture/planning/DIVERGENCE_LEDGER.md`, add above the D-013 row:

```markdown
| D-014 | Deck/squad system | 3/10 | Squad = fixed uint32[3], 0=empty (Squad.h:34); ResetSquads = empty decks (User.cpp:247-258, SignUp fill is a TODO-remove hack API.cpp:808-814); UpdateSquad validates ownership + compacts (User.cpp:260-289); WriteSquadsAPI skips zero slots (User.cpp:589-620); SetSquad: empty slot -> null character, no completeness check (Player.cpp:267-313) | DeckService/GameService/GameRestController | Rebuilt to the C++ contract: virgin accounts get 3 EMPTY decks; updateDecks ownership-validated, active-slot-only, 3 positional values; ResolveSquad injection fallback DELETED; CreatureIds SQLite round-trip locked by test. Fixes: seeded creature re-appearing in edited decks + "squad incomplete" after relaunch. Spec: docs/superpowers/specs/2026-06-04-deck-system-design.md. | fixed | |
```

- [ ] **Step 3: Point the old deck doc at the new spec**

At the top of `docs/architecture/deck-system/DECK_SQUAD_SYSTEM.md` (right under the title), insert:

```markdown
> **2026-06-04:** The deck/squad server behavior was rebuilt to the verified C++ contract —
> see `docs/superpowers/specs/2026-06-04-deck-system-design.md` (authoritative) and
> DIVERGENCE_LEDGER D-014. Historical content below may describe the older implementation.
```

- [ ] **Step 4: Commit docs**

```powershell
git add docs/architecture/planning/DIVERGENCE_LEDGER.md docs/architecture/deck-system/DECK_SQUAD_SYSTEM.md
git commit -m "docs(deck): D-014 ledger row + pointer to the deck-system spec"
```

- [ ] **Step 5: Manual gate (user-run, client required)**

1. Stop the server. Delete the dev DB so stale seeded decks vanish: `ReCap.Server/bin/Debug/net10.0/server.db` (disposable test data; required if Task 1 Step 5 changed the storage format).
2. Rebuild + start the server; create a fresh account in the client.
3. Verify in the client: 3 decks, ALL EMPTY (no injected creature anywhere).
4. Build a 3-creature squad in deck 1 (client editor); confirm `api.deck.updateDecks` appears in the server log.
5. Close the client AND restart the server (full persistence path), reopen, log in.
6. Verify deck 1 still holds exactly the 3 chosen creatures — no random creature, no "squad incomplete".
7. Enter the dungeon: the deployed heroes are the 3 chosen creatures.

Memory write-back after the gate passes (CLAUDE.md rule): update `deck-squad` related memory + MEMORY.md with the outcome.
