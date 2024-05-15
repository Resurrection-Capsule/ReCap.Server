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

    public List<Deck> getDecksByAccountId(ulong accountId)
    {
        var deckModels = sqliteConfig.Decks.Where(b => b.AccountID == accountId).ToList();
        return deckModels.Select(deckModel => deckMapper.toDomain(deckModel)).ToList();
    }

    public void insertDeck(Deck deck)
    {
        var deckModel = deckMapper.toModel(deck);
        sqliteConfig.Decks.Add(deckModel);
        sqliteConfig.SaveChanges();
    }
}
