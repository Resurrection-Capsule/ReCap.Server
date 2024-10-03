using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;

using HttpServer;

namespace ReCap.Server.Config.Server;

public class ServerConfig
{
    public static string GetHost()
    {
        return "localhost";
    }

    public static string GetDarksporeVersion()
    {
        return "5.3.0.127";
    }

    public static string GetServerDatabasePath()
    {
        var path = AppDomain.CurrentDomain.BaseDirectory;
        return System.IO.Path.Join(path, "server.db");
    }
}
