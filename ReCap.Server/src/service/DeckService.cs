namespace HttpServer;

using HttpServer;

using ReCap.Server.Config.Sqlite;
using ReCap.Server.Domain.Deck;
using ReCap.Server.Model.Account;
using ReCap.Server.Model.Deck;

public class DeckService
{
    private DeckRepositoryAdapter deckRepository;

    public DeckService(SqliteConfig newSqliteConfig) {
        deckRepository = new DeckRepositoryAdapter(newSqliteConfig);
    }

    public List<DeckModel> getDecksByAccount(AccountModel account) {
        return deckRepository.getDecksByAccountId(account.Id);
    }

    public List<Deck> createDecksForAccount(AccountModel account) {
        List<Deck> decks = [];
        for (ulong squadSlot = 1; squadSlot <= 3; squadSlot++) {
            var deck = new Deck{
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