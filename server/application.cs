using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer;

public class Application
{
    static void Main(string[] args)
    {
        var dbConfig = new SqliteConfig();
        var restClientAdapter = new RestClientAdapter(dbConfig);
        restClientAdapter.Run();
    }
}
