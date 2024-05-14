using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;
using HttpMultipartParser;

namespace HttpServer;

public class GameRestClientAdapter
{
    private AccountMapper accountMapper;
    private AccountService accountService;
    private CreatureMapper creatureMapper;
    private CreatureService creatureService;

    public GameRestClientAdapter(SqliteConfig newSqliteConfig) {
        accountMapper = new AccountMapper();
        accountService = new AccountService(newSqliteConfig);
        creatureMapper = new CreatureMapper();
        creatureService = new CreatureService(newSqliteConfig);
    }

    [ApiMethod(Name="api.account.auth")]
    public byte[] loginPlayerAccount(HttpListenerContext context)
    {
        var request = context.Request;
        var parameters = request.QueryString;

        string authToken = null;
        Account account = null;

        var parser = MultipartFormDataParser.Parse(request.InputStream);
        string key = parser.GetParameterValue("key");
        if (key != null) {
            // key = auth_token::0
            string[] keyParts = key.Split(' ');
            authToken = keyParts[0];
            account = accountService.getAccountByAuthToken(authToken);
        }
        if (account == null) {
            throw new ForbiddenOperationException("Unindentified account");
        }

        // auto newPlayerProgress = request.uri.parameter<uint32_t>("new_player_progress");
        // if (newPlayerProgress != 0) {
        //     std::cout << "Old progress: " << account.newPlayerProgress << ", New: " << newPlayerProgress << std::endl;
        //     account.newPlayerProgress = newPlayerProgress;
        // }

        // account.Write(docAccount);

        var response = new AuthResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1,
            Account = accountMapper.toContract(account)
        };

        bool includeCreatures = Convert.ToBoolean(parser.GetParameterValue("include_creatures"));
        bool includeDecks = Convert.ToBoolean(parser.GetParameterValue("include_decks"));
        bool includeFeed = Convert.ToBoolean(parser.GetParameterValue("include_feed"));
        bool includeSettings = Convert.ToBoolean(parser.GetParameterValue("include_settings"));
        bool includeServerTuning = Convert.ToBoolean(parser.GetParameterValue("include_server_tuning"));
        bool includeTokenCookie = Convert.ToBoolean(parser.GetParameterValue("cookie"));

        if (includeCreatures) {
            response.Creatures = creatureService.getCreaturesByAccount(account).Select(creature => creatureMapper.toContract(creature)).ToList();
        }

        if (includeDecks) {
            // TODO: Not implemented
            response.Decks = [];
        }

        if (includeFeed) {
            // TODO: Not implemented
            response.Feed = new FeedContract{ Items = [] };
        }

        if (includeSettings) {
            response.Settings = new SettingsContract{
                ShowConfigAlerts = true ? "on" : "off",
                Cheat = true ? "on" : "off",
                SafeMode = true ? "on" : "off"
            };
        }

        if (includeServerTuning) {
            int timestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            response.ServerTuning = new ServerTuningContract{
                ItemstoreOfferPeriod = timestamp,
                ItemstoreCurrentExpiration = timestamp + (3 * 60 * 60 * 1000),
                ItemstoreCostMultiplierBasic = 1,
                ItemstoreCostMultiplierUncommon = 1.1,
                ItemstoreCostMultiplierRare = 1.2,
                ItemstoreCostMultiplierEpic = 1.3,
                ItemstoreCostMultiplierUnique = 1.4,
                ItemstoreCostMultiplierRareUnique = 1.5,
                ItemstoreCostMultiplierEpicUnique = 1.6
            };
        }

        if (includeTokenCookie) {
            context.Response.SetCookie(new Cookie("token", authToken));
        }

        return XmlUtils.Serialize(response);
    }

    [ApiMethod(Name="api.account.getAccount")]
    public byte[] getPlayerAccount(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.account.logout")]
    public byte[] logoutPlayerAccount(HttpListenerContext context)
    {
        var response = new ResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlUtils.Serialize(response);
    }

    [ApiMethod(Name="api.account.searchAccounts")]
    public byte[] searchPlayerAccounts(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.account.setSettings")]
    public byte[] setPlayerAccountSettings(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.account.unlock")]
    public byte[] unlockPlayerAccount(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.account.setNewPlayerStats")]
    public byte[] setNewPlayerStats(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.creature.getCreature")]
    public byte[] getCreature(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.creature.getTemplate")]
    public byte[] getTemplate(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.creature.resetCreature")]
    public byte[] resetCreature(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.creature.unlockCreature")]
    public byte[] unlockCreature(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.creature.updateCreature")]
    public byte[] updateCreature(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.deck.updateDecks")]
    public byte[] updateDecks(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.game.exitGame")]
    public byte[] exitGame(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.game.getGame")]
    public byte[] getGame(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.game.getRandomGame")]
    public byte[] getRandomGame(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.inventory.getPartList")]
    public byte[] getPartList(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.inventory.getPartOfferList")]
    public byte[] getPartOfferList(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.inventory.updatePartStatus")]
    public byte[] updatePartStatus(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.inventory.vendorParts")]
    public byte[] getVendorParts(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.leaderboard.getLeaderboard")]
    public byte[] getLeaderboard(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        return null;
    }

    [ApiMethod(Name="api.status.getBroadcastList")]
    public byte[] getBroadcastList(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        var response = new StatusResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1,
            Broadcasts = BroadcastService.getBroadcastList()
        };

        return XmlUtils.Serialize(response);
    }

    [ApiMethod(Name="api.status.getStatus")]
    public byte[] getStatus(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;
        bool includeBroadcasts = parameters.Get("include_broadcasts") == "true";

        var response = new StatusResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1,
            Status = StatusService.getStatus()
        };

        if (includeBroadcasts)
        {
            response.Broadcasts = BroadcastService.getBroadcastList();
        }

        return XmlUtils.Serialize(response);
    }
}
