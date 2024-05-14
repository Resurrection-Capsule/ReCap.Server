using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer;

public class ReCapRestClientAdapter
{
    private AccountService accountService;

    public ReCapRestClientAdapter(SqliteConfig newSqliteConfig) {
        accountService = new AccountService(newSqliteConfig);
    }

    private string getBodyFromRequest(HttpListenerContext context)
    {
        var request = context.Request;
        if (!request.HasEntityBody)
        {
            return "";
        }
        System.IO.Stream body = request.InputStream;
        System.Text.Encoding encoding = request.ContentEncoding;
        System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);
        string s = reader.ReadToEnd();
        body.Close();
        reader.Close();
        return s;
    }

    [ApiMethod(Name="api.game.log")]
    public byte[] log(HttpListenerContext context)
    {
        string message = getBodyFromRequest(context);
        Console.WriteLine(message);
        return new byte[]{};
    }

    [ApiMethod(Name="api.game.registration")]
    public byte[] registerUser(HttpListenerContext context)
    {
        var request = context.Request;
        var parameters = request.QueryString;
        string email = parameters.Get("email");
        string name = parameters.Get("name");
        string password = parameters.Get("pass");
        int avatar = Int32.Parse(parameters.Get("avatar"));

        accountService.createAccount(email, name, password, avatar);

        var response = new ResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlUtils.Serialize(response);
    }
}
