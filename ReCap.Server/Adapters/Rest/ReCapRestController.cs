
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using ReCap.Server.Config;
using ReCap.Server.Services;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.Rest.Api;

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
        string message = HTTPHelper.GetBodyFromRequest(context.Request);
        ReCap.Server.Util.Logging.Log.Rest.Info(message);
        return new byte[]{};
    }

    [RequestMapping(Name="api.game.registration")]
    public byte[] registerUser(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        // string jsonStr = HTTPHelper.GetBodyFromRequest(context.Request);
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
            deckService.createDecksForAccount(account.Id);
            creaturePartService.addAllCreatureParts(account);

            accountService.updateAccount(account);
        }

        return Encoding.ASCII.GetBytes("{\"success\":true}");
    }

    [RequestMapping(Name="api.game.getCreatureLargePng")]
    public byte[] getCreatureLargePng(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        ulong creatureId = (ulong)Convert.ToInt32(parameters["id"]);
        var creature = creatureService.getCreatureById(creatureId)
            ?? throw new ForbiddenOperationException("Creature not found");
        // We only ship per-template thumbnails, so large falls back to the thumb too.
        return CreatureImage(creature.LargePngBase64, creature.TemplateID);
    }

    [RequestMapping(Name="api.game.getCreatureThumbPng")]
    public byte[] getCreatureThumbPng(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        ulong creatureId = (ulong)Convert.ToInt32(parameters["id"]);
        var creature = creatureService.getCreatureById(creatureId)
            ?? throw new ForbiddenOperationException("Creature not found");
        return CreatureImage(creature.ThumbPngBase64, creature.TemplateID);
    }

    // Custom (edited) png if present, otherwise the creature's template thumbnail.
    private static byte[] CreatureImage(string? customBase64, ulong templateId)
    {
        if (!string.IsNullOrEmpty(customBase64))
            return Convert.FromBase64String(customBase64);

        var path = Path.Combine(ServerConfig.ResourcesDirectory, "static", "template_png", $"{templateId}_thumb.png");
        return File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
    }
}
