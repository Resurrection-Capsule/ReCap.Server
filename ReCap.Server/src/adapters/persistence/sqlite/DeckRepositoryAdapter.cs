using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer;

public class DeckRepositoryAdapter
{
    private SqliteConfig sqliteConfig;
    private DeckMapper deckMapper;
    private Random sequenceRandomGenerator;

    public DeckRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        deckMapper = new DeckMapper();
        sequenceRandomGenerator = new Random();
    }

    public List<DeckModel> getDecksByAccountId(ulong accountId)
    {
        return sqliteConfig.Decks.Where(b => b.AccountID == accountId).ToList();
    }

    public void insertDeck(Deck deck)
    {
        deck.ID = (ulong)sequenceRandomGenerator.Next(10000000); // TODO: Generate ID dynamically

        var deckModel = deckMapper.toModel(deck);
        sqliteConfig.Decks.Add(deckModel);
        sqliteConfig.SaveChanges();
    }
}
