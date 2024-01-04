using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer
{
    class ServerConfig
    {
        public static string GetHost()
        {
            return "localhost:8080";
        }

        public static string GetDarksporeVersion()
        {
            return "5.3.0.127";
        }
    }
}
