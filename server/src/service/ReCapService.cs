using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer
{
    class ReCapService
    {
        [ApiMethod(Name="api.game.registration")]
        public static byte[] registerUser(NameValueCollection parameters)
        {
            return null;
        }
    }
}
