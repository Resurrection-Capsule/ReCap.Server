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

namespace HttpServer
{
    class LauncherService
    {
        public static byte[] HandleRequest(HttpListenerContext context)
        {
            string method = context.Request.QueryString.Get("method");

            if (method == "api.config.getConfigs")
            {
                return SerializeAsXml(getConfigs(context.Request.QueryString));
            }

            return null;
        }

        private static byte[] SerializeAsXml<T>(T value)
        {
            var xmlserializer = new XmlSerializer(typeof(T));
            var stringWriter = new StringWriter();
            using (var writer = XmlWriter.Create(stringWriter))
            {
                xmlserializer.Serialize(writer, value);
                string xmlStr = stringWriter.ToString();
                return Encoding.UTF8.GetBytes(xmlStr);
            }
        }

        private static ConfigResponseContract getConfigs(NameValueCollection parameters) {
            bool includeSettings = parameters.Get("include_settings") == "true";
            bool includePatches = parameters.Get("include_patches") == "true";

            string host = ServerConfig.GetHost();
            string darksporeVersion = ServerConfig.GetDarksporeVersion();

            var config = new ConfigContract{
                BlazeServiceName = "darkspore", // Directly linked to BlazeServiceName
				BlazeSecure = "N", // Directly linked to BlazeSecure
				BlazeEnv = "prod", // Directly linked to BlazeEnvironment, can be { prod, beta, cert, test, dev }
				SporenetCdnHost = host,
				SporenetDbHost = host,
				SporenetDbName = "darkspore",
				SporenetHost = host,
				HttpSecure = "N",
				LiferayHost = host,
				LauncherAction = 2,
				LauncherUrl = "http://" + host + "/bootstrap/launcher/?version=" + darksporeVersion
            };

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
                response.Patches = new ConfigPatchesContract{
                    Target = "test",
                    Date = "test2",
                    FromVersion = "test3",
                    ToVersion = "test4",
                    ID = "test5",
                    Description = "test6",
                    ApplicationInstructions = "test6",
                    Locale = "en-US",
                    Shipping = "true",
                    FileUrl = "test.zip",
                    ArchiveSize = "1000",
                    UncompressedSize = "2000",
                    Hashes = "0123456789abcdef",
                    HashesList = new string[] {}
                };
            }

            return response;
        }
    }
}
