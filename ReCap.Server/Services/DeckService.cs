using ReCap.Server.Adapters.Persistence.SQLite;
using ReCap.Server.Config;
using ReCap.Server.Domain;
using ReCap.Server.Models;

namespace ReCap.Server.Services;

public class DeckService
{
    private DeckRepositoryAdapter deckRepository;

    public DeckService(SqliteConfig newSqliteConfig) {
        deckRepository = new DeckRepositoryAdapter(newSqliteConfig);
    }

    public List<DeckModel> getDecksByAccount(AccountModel account) {
        return deckRepository.getDecksByAccountId(account.Id);
    }

    // Persist a squad/deck edit (api.deck.updateDecks). slot is the 1-based squad id.
    public void updateDeck(AccountModel account, int slot, List<ulong> creatureIds) {
        var deck = deckRepository.getDecksByAccountId(account.Id).FirstOrDefault(d => d.Slot == slot);
        if (deck == null) return;
        deck.CreatureIds = creatureIds;
        deckRepository.updateDeck(deck);
    }

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
}