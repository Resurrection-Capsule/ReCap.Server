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
    private CreatureService creatureService;
    private DeckService deckService;

    public ReCapRestClientAdapter(SqliteConfig newSqliteConfig) {
        accountService = new AccountService(newSqliteConfig);
        creatureService = new CreatureService(newSqliteConfig);
        deckService = new DeckService(newSqliteConfig);
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
        bool isTest = true; // TODO: Unlocking everything from start to test; make that configurable through parameters

        var account = accountService.createAccount(email, name, password, avatar, isTest);

        // TODO: Initial parts
        // auto actualCreaturePartsSize = Repository::CreatureCreatureParts::ListAll().size();
		// auto creatureCreatureParts = Repository::CreatureParts::ListAll();
		// uint64_t index = 1;
		// for (auto& creatureCreaturePart : creatureCreatureParts) {
		// 	Repository::CreatureCreatureParts::Add(std::make_shared<Game::CreatureCreaturePart>(actualCreaturePartsSize + index++,
        //      creatureCreaturePart->rigblock_asset_id, user->get_account().id));
		// }
		// Repository::CreatureCreatureParts::Save();

		if (isTest) {
            var creatures = creatureService.addAllCreatures(account);
            var decks = deckService.createDecksForAccount(account);
        }

        var response = new ResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlUtils.Serialize(response);
    }
}
