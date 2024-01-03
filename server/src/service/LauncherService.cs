using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

using HttpServer;

namespace HttpServer
{
    class LauncherService
    {
        public static byte[] HandleRequest(HttpListenerContext context)
        {
            var queryString = context.Request.QueryString;
            string method = queryString.Get("method");

            if (method == "api.config.getConfigs")
            {
                bool includeSettings = queryString.Get("include_settings") == "true";
                bool includePatches = queryString.Get("include_patches") == "true";
                return getConfigs(includeSettings, includePatches);
            }

            return null;
        }

        private static byte[] getConfigs(bool includeSettings, bool includePatches) {
            string host = ServerConfig.GetHost();
            return null;
        }
    }
}
