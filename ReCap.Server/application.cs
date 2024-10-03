namespace ReCap.Server.Application;

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using BlazeServer;

using ReCap.RakNetServer;

using ReCap.Server.Adapters.Rest.Api;
using ReCap.Server.Config.Sqlite;

public class Application
{
    static void Main(string[] args)
    {
        string hostname = "localhost";

        CancellationTokenSource source = new CancellationTokenSource();
        CancellationToken token = source.Token;

        var dbConfig = new SqliteConfig();
        dbConfig.Start();

        Task.Run(() => {
            var blazeHttpServer = new Server(dbConfig, "Redirector", IPAddress.Parse("127.0.0.1"), 42127, true, hostname);
            blazeHttpServer.Start();
        });

        Task.Run(() => {
            var blazeHttpServer = new Server(dbConfig, "Lobby", IPAddress.Parse("127.0.0.1"), 42125, false, hostname);
            blazeHttpServer.Start();
        });

        Task.Run(() => {
            var rakNetServer = new RakNetServer(dbConfig, "RakNet", IPAddress.Parse("127.0.0.1"), 42000, false, hostname);
            rakNetServer.ExecuteAsync(token);
        });

        var restClientAdapter = new Api(dbConfig);
        restClientAdapter.Run();
    }
}
