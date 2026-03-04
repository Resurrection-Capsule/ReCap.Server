using System.Net;
using HttpMultipartParser;

namespace ReCap.Server.Util;

public class HTTPHelper
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

    public static Dictionary<string,string> GetParametersFromRequest(HttpListenerRequest request)
    {
        var parameters = new Dictionary<string,string>();

        // Query parameters
        var query = request.QueryString;
        foreach (string key in query.Keys) {
            parameters.Add(key, query.Get(key));
        }

        // Multipart form parameters
        if (request.HttpMethod == "POST") {
            try {
                var inputStream = request.InputStream;
                var parser = MultipartFormDataParser.Parse(inputStream);
                foreach(var entry in parser.Parameters) {
                    parameters.Add(entry.Name, entry.Data);
                }
            }
            catch (Exception ex) {}
        }

        // Cookies parameters
        var cookies = request.Cookies;
        foreach(Cookie cookie in cookies) {
            if (parameters.ContainsKey(cookie.Name)) {
                if (parameters[cookie.Name] == "cookie") {
                    parameters[cookie.Name] = cookie.Value;
                }
            }
        }

        return parameters;
    }
}
