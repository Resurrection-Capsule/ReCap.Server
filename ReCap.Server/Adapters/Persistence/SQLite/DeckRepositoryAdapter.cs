using System;
using System.IO;
using System.Net;
using System.Text;
using ReCap.Server.Config;
using ReCap.Server.Domain;
using ReCap.Server.Mappers;
using ReCap.Server.Models;

namespace ReCap.Server.Adapters.Persistence.SQLite;

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
