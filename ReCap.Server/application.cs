using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using HttpServer;
using BlazeServer;

namespace HttpServer;

public class Application
{
    static void Main(string[] args)
    {
        var dbConfig = new SqliteConfig();
        dbConfig.Start();

        Task.Run(() => {
            var blazeHttpServer = new Server(dbConfig, "Redirector", IPAddress.Parse("127.0.0.1"), 42127, true, "localhost");
            blazeHttpServer.Start();
        });

        Task.Run(() => {
            var blazeHttpServer = new Server(dbConfig, "Lobby", IPAddress.Parse("127.0.0.1"), 42125, false, "localhost");
            blazeHttpServer.Start();
        });

        var restClientAdapter = new Api(dbConfig);
        restClientAdapter.Run();
    }
}
