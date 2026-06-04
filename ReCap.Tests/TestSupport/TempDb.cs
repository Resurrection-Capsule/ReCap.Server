using System;
using System.IO;

namespace ReCap.Tests.TestSupport;

internal static class TempDb
{
    public static string NewPath() =>
        Path.Combine(Path.GetTempPath(), $"recap-test-{Guid.NewGuid():N}.db");

    public static void TryDelete(string path)
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
        catch { /* temp dir, best effort */ }
    }
}
