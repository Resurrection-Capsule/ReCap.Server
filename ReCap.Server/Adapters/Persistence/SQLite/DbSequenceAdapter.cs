using System;
using System.IO;
using System.Net;
using System.Text;

using ReCap.Server.Config;
using ReCap.Server.Models;

namespace ReCap.Server.Adapters.Persistence.SQLite;

public class DbSequenceAdapter
{
    private SqliteConfig sqliteConfig;

    public DbSequenceAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
    }

    public int Next(string sequenceName, int count = 1)
    {
        DbSequenceModel? sequence = sqliteConfig.DbSequences.SingleOrDefault(b => b.ID == sequenceName);
        if (sequence is null) {
            sequence = new DbSequenceModel{
                ID = sequenceName,
                Value = 0
            };
            sqliteConfig.DbSequences.Add(sequence);
        }
        sequence.Value += count;
        sqliteConfig.SaveChanges();
        return sequence.Value;
    }
}
