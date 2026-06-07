using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.Collections.Specialized;

using ReCap.Server.Adapters.Rest.Contracts.Bootstrap;
using ReCap.Server.Config;
using ReCap.Server.Services;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.Rest.Api;

[RestController(Value="/bootstrap/api", ContentType="text/xml")]
public class BootstrapRestController
{
    public BootstrapRestController(SqliteConfig newSqliteConfig) {}

    [RequestMapping(Name="api.config.getConfigs")]
    public byte[] getConfigs(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string? darksporeVersion = parameters.GetValueOrDefault("build");
        bool includeSettings = parameters.GetValueOrDefault("include_settings") == "true";
        bool includePatches = parameters.GetValueOrDefault("include_patches") == "true";

        string host = ServerConfig.HostName;

        var config = ConfigService.getGameConfig(darksporeVersion);

        var configs = new List<ConfigContract>(){config};
        var response = new ConfigResponseContract{
            Stat = "ok",
            Version = darksporeVersion,
            Timestamp = 1,
            ExecTime = 1,
            Configs = configs,
            ToImage = "",
            FromImage = ""
        };

        if (includeSettings)
        {
            response.Settings = new ConfigSettingsContract{
                Open=new ConfigSettingsOpenContract{Value=true, Test=true},
                TelemetryRate=256, TelemetrySetting=0
            };
        }
        if (includePatches)
        {
            // No patches are served (the client picks its own locale; server-sent locale is ignored).
        }

        return XmlHelper.Serialize(response);
    }
}
