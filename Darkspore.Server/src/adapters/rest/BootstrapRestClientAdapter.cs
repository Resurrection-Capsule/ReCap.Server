using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer;

public class BootstrapRestClientAdapter
{
    [ApiMethod(Name="api.config.getConfigs")]
    public byte[] getConfigs(NameValueCollection parameters) {
        bool includeSettings = parameters.Get("include_settings") == "true";
        bool includePatches = parameters.Get("include_patches") == "true";

        string host = ServerConfig.GetDarksporeHosts()[0];
        string darksporeVersion = ServerConfig.GetDarksporeVersion();

        var config = ConfigService.getGameConfig();

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
            // response.Patches = new ConfigPatchesContract{
            //     Target = "test",
            //     Date = "test2",
            //     FromVersion = "test3",
            //     ToVersion = "test4",
            //     ID = "test5",
            //     Description = "test6",
            //     ApplicationInstructions = "test6",
            //     Locale = "en-US",
            //     Shipping = "true",
            //     FileUrl = "test.zip",
            //     ArchiveSize = "1000",
            //     UncompressedSize = "2000",
            //     Hashes = "0123456789abcdef",
            //     HashesList = new string[] {}
            // };
        }

        return XmlUtils.Serialize(response);
    }
}
