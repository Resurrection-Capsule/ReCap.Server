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

    [ApiMethod(Name="api.game.log")]
    public static byte[] log(NameValueCollection parameters)
    {
        string message = parameters.Get("message");
        Console.WriteLine(message);
        return new byte[]{};
    }

    [ApiMethod(Name="api.game.registration")]
    public byte[] registerUser(NameValueCollection parameters)
    {
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
