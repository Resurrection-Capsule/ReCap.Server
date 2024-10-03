using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

using ReCap.Server.Adapters.Persistence.SQLite.DbSequence;
using ReCap.Server.Config.Sqlite;
using ReCap.Server.Domain.Deck;
using ReCap.Server.Model.Deck;

namespace HttpServer;

public class DeckRepositoryAdapter
{
    private static string DECK_SEQUENCE_NAME = "DECK_SEQUENCE";

    private SqliteConfig sqliteConfig;
    private DeckMapper deckMapper;
    private DbSequenceAdapter sequenceRandomGenerator;

    public DeckRepositoryAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
        deckMapper = new DeckMapper();
        sequenceRandomGenerator = new DbSequenceAdapter(newSqliteConfig);
    }

    public List<DeckModel> getDecksByAccountId(ulong accountId)
    {
        return sqliteConfig.Decks.Where(b => b.AccountID == accountId).ToList();
    }

    public void insertDeck(Deck deck)
    {
        deck.ID = (ulong)sequenceRandomGenerator.Next(DECK_SEQUENCE_NAME);

        var deckModel = deckMapper.toModel(deck);
        sqliteConfig.Decks.Add(deckModel);
        sqliteConfig.SaveChanges();
    }
}
