using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer;

public class DbSequenceAdapter
{
    private SqliteConfig sqliteConfig;

    public DbSequenceAdapter(SqliteConfig newSqliteConfig) {
        sqliteConfig = newSqliteConfig;
    }

    public int Next(string sequenceName)
    {
        DbSequenceModel sequence = sqliteConfig.DbSequences.SingleOrDefault(b => b.ID == sequenceName);
        if (sequence is null) {
            sequence = new DbSequenceModel{
                ID = sequenceName,
                Value = 0
            };
            sqliteConfig.DbSequences.Add(sequence);
        }
        sequence.Value += 1;
        sqliteConfig.SaveChanges();
        return sequence.Value;
    }
}
