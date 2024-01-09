using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;

using HttpServer;

namespace HttpServer;

public class ServerConfig
{
    public static string[] GetDarksporeHosts()
    {
        return new string[]{
            "config.darkspore.com",
            "gosredirector.online.ea.com",
            "gosredirector.ea.com",
            "api.darkspore.com",
            "content.darkspore.com",
            "beta.darkspore.ea.com",
            "beta-sn.darkspore.ea.com",
            "beta-sn2.darkspore.ea.com",
            "dev.darkspore.ea.com",
            "dev-sn.darkspore.ea.com",
            "dev-sn2.darkspore.ea.com",
            "fail.spore.rws.ad.ea.com",
            "ea6.com.edgesuite.net",
            "darkspore.alpha.lockbox.ea.com",
            "www.sporelabs.com",
            "splabbetamydb1b.rspc-iad.ea.com",
            "321917-prodmydb009.spore.rspc-iad.ea.com",
            "telemetry.maxis.com"
        };
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
