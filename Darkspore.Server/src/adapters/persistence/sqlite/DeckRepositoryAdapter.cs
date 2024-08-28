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

    public DeckRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        deckMapper = new DeckMapper();
    }

    public List<DeckModel> getDecksByAccountId(ulong accountId)
    {
        return sqliteConfig.Decks.Where(b => b.AccountID == accountId).ToList();
    }

    public void insertDeck(Deck deck)
    {
        var deckModel = deckMapper.toModel(deck);
        sqliteConfig.Decks.Add(deckModel);
        sqliteConfig.SaveChanges();
    }
}
