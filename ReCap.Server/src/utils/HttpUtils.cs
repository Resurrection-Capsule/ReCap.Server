using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer;

public class HttpUtils
{
    public static string GetBodyFromRequest(HttpListenerRequest request)
    {
        System.IO.Stream body = request.InputStream;
        System.Text.Encoding encoding = request.ContentEncoding;
        System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);
        string s = reader.ReadToEnd();
        body.Close();
        reader.Close();
        return s;
    }
}
