namespace HttpServer;

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using ReCap.Server.Config.Sqlite;
using ReCap.Server.Utils.Logger;

using HttpServer;

[RestController(Value="/recap/api")]
public class ReCapRestController
{
    private AccountService accountService;
    private CreatureService creatureService;
    private CreaturePartService creaturePartService;
    private DeckService deckService;

    public ReCapRestController(SqliteConfig newSqliteConfig) {
        accountService = new AccountService(newSqliteConfig);
        creatureService = new CreatureService(newSqliteConfig);
        creaturePartService = new CreaturePartService(newSqliteConfig);
        deckService = new DeckService(newSqliteConfig);
    }

    [RequestMapping(Name="api.game.log", ContentType="application/json")]
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
            creatureService.addAllCreatures(account);
            deckService.createDecksForAccount(account);
            creaturePartService.addAllCreatureParts(account);

            accountService.updateAccount(account);
        }

        return Encoding.ASCII.GetBytes("{\"success\":true}");
    }

    [RequestMapping(Name="api.game.getCreatureLargePng")]
    public byte[] getCreatureLargePng(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        ulong creatureId = (ulong)Convert.ToInt32(parameters["id"]);
        var creature = creatureService.getCreatureById(creatureId);
        var base64 = creature.LargePngBase64;
        return Convert.FromBase64String(base64);
    }

    [RequestMapping(Name="api.game.getCreatureThumbPng")]
    public byte[] getCreatureThumbPng(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        ulong creatureId = (ulong)Convert.ToInt32(parameters["id"]);
        var creature = creatureService.getCreatureById(creatureId);
        var base64 = creature.ThumbPngBase64;
        return Convert.FromBase64String(base64);
    }
}
