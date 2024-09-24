using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;
using LoggerUtil;

namespace HttpServer;

[RestController(Value="/recap/api", ContentType="application/json")]
public class ReCapRestClientAdapter
{
    private AccountService accountService;
    private CreatureService creatureService;
    private CreaturePartService creaturePartService;
    private DeckService deckService;

    public ReCapRestClientAdapter(SqliteConfig newSqliteConfig) {
        accountService = new AccountService(newSqliteConfig);
        creatureService = new CreatureService(newSqliteConfig);
        creaturePartService = new CreaturePartService(newSqliteConfig);
        deckService = new DeckService(newSqliteConfig);
    }

    [RequestMapping(Name="api.game.log")]
    public byte[] log(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string message = HttpUtils.GetBodyFromRequest(context.Request);
        Logger.info(message);
        return new byte[]{};
    }

    [RequestMapping(Name="api.game.registration")]
    public byte[] registerUser(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        // string jsonStr = HttpUtils.GetBodyFromRequest(context.Request);
        // dynamic request = JsonConvert.DeserializeObject(jsonStr);
        
        var request = context.Request;
        string email = parameters["email"];
        string name = parameters["name"];
        string password = parameters["pass"];
        int avatar = Int32.Parse(parameters["avatar"]);
        bool isTest = true; // TODO: Unlocking everything from start to test; make that configurable through parameters

        var account = accountService.createAccount(email, name, password, avatar, isTest);

		if (isTest) {
            var creatures = creatureService.addAllCreatures(account);
            var decks = deckService.createDecksForAccount(account);
            var parts = creaturePartService.addAllCreatureParts(account);
        }

        return Encoding.ASCII.GetBytes("{\"success\":true}");
    }
}
