using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using HttpServer;

namespace HttpServer;

public class Application
{
    static void Main(string[] args)
    {
        var dbConfig = new SqliteConfig();

        var blazeHttpServer = new BlazeHttpServer();

        Task.Run(() => {
            blazeHttpServer.Start();
        });

        var restClientAdapter = new RestClientAdapter(dbConfig);
        restClientAdapter.Run();
    }
}
