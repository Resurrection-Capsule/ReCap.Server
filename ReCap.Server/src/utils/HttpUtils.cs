using HttpServer;

namespace HttpServer;

public class HttpUtils
{
    public static string GetBodyFromRequest(System.Net.HttpListenerRequest request)
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
