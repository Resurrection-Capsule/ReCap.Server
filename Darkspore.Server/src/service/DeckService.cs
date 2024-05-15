using HttpServer;

namespace HttpServer;

public class DeckService
{
    private DeckRepositoryAdapter deckRepository;

    public DeckService(SqliteConfig newSqliteConfig) {
        deckRepository = new DeckRepositoryAdapter(newSqliteConfig);
    }

    public List<Deck> getDecksByAccount(Account account) {
        return deckRepository.getDecksByAccountId(account.Id);
    }

    public List<Deck> createDecksForAccount(Account account) {
        List<Deck> decks = [];
        for (ulong squadSlot = 1; squadSlot <= 3; squadSlot++) {
            var deck = new Deck{
                ID = squadSlot, // TODO: Generate ID dynamically
                Name = "Slot " + squadSlot.ToString(),
                Slot = (int)squadSlot,
                Category = "pve",
                AccountID = account.Id,
                CreatureIds = []
            };
            deckRepository.insertDeck(deck);
            decks.Add(deck);
        }
        return decks;
    }
}