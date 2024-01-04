using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer
{
    class GameService
    {
        [ApiMethod(Name="api.account.auth")]
        public static byte[] authenticateAccount(NameValueCollection parameters)
        {
            return null;
        }
    }
}
