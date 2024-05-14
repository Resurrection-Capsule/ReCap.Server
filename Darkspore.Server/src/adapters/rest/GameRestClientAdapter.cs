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
    private AccountService accountService;
    private AccountMapper accountMapper;

    public GameRestClientAdapter(SqliteConfig newSqliteConfig) {
        accountMapper = new AccountMapper();
        accountService = new AccountService(newSqliteConfig);
    }

    [ApiMethod(Name="api.account.auth")]
    public byte[] loginPlayerAccount(HttpListenerContext context)
    {
        var request = context.Request;
        var parameters = request.QueryString;

        Account account = null;

        var parser = MultipartFormDataParser.Parse(request.InputStream);
        string key = parser.GetParameterValue("key");
        if (key != null) {
            // key = auth_token::0
            string[] keyParts = key.Split(' ');
            string authToken = keyParts[0];

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

        bool includeCreatures = Convert.ToBoolean(parameters.Get("include_creatures"));
        bool includeDecks = Convert.ToBoolean(parameters.Get("include_decks"));
        bool includeFeed = Convert.ToBoolean(parameters.Get("include_feed"));
        bool includeSettings = Convert.ToBoolean(parameters.Get("include_settings"));
        bool includeServerTuning = Convert.ToBoolean(parameters.Get("include_server_tuning"));
        bool includeTokenCookie = Convert.ToBoolean(parameters.Get("cookie"));

        if (includeCreatures) {
            Console.WriteLine("[GameRestClientAdapter] includeCreatures");
            // user->get_creatures().WriteApi(docResponse);
        }

        if (includeDecks) {
            Console.WriteLine("[GameRestClientAdapter] includeDecks");
            // user->WriteSquadsAPI(docResponse);
        }

        if (includeFeed) {
            Console.WriteLine("[GameRestClientAdapter] includeFeed");
            // user->get_feed().Write(docResponse);
        }

        if (includeSettings) {
            Console.WriteLine("[GameRestClientAdapter] includeSettings");
        //     if (auto settingsDoc = docResponse.append_child("settings")) {
        //         // Values can be an integer(long) or "on/off"
        //         /*
        //         utils::xml_add_text_node(settingsDoc, "showConfigAlerts", "on");
        //         utils::xml_add_text_node(settingsDoc, "cheat", "on");
        //         utils::xml_add_text_node(settingsDoc, "safeMode", "on");
        //         */
        //     }
        }

        if (includeServerTuning) {
            Console.WriteLine("[GameRestClientAdapter] includeServerTuning");
        //     if (auto server_tuning = docResponse.append_child("server_tuning")) {
        //         utils::xml_add_text_node(server_tuning, "itemstore_offer_period", timestamp);
        //         utils::xml_add_text_node(server_tuning, "itemstore_current_expiration", timestamp + (3 * 60 * 60 * 1000));
        //         utils::xml_add_text_node(server_tuning, "itemstore_cost_multiplier_basic", 1);
        //         utils::xml_add_text_node(server_tuning, "itemstore_cost_multiplier_uncommon", 1.1);
        //         utils::xml_add_text_node(server_tuning, "itemstore_cost_multiplier_rare", 1.2);
        //         utils::xml_add_text_node(server_tuning, "itemstore_cost_multiplier_epic", 1.3);
        //         utils::xml_add_text_node(server_tuning, "itemstore_cost_multiplier_unique", 1.4);
        //         utils::xml_add_text_node(server_tuning, "itemstore_cost_multiplier_rareunique", 1.5);
        //         utils::xml_add_text_node(server_tuning, "itemstore_cost_multiplier_epicunique", 1.6);
        //     }
        }

        if (includeTokenCookie) {
            Console.WriteLine("[GameRestClientAdapter] includeTokenCookie");
        //     response.set(boost::beast::http::field::set_cookie, "token=" + user->get_auth_token());
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
        var parameters = context.Request.QueryString;
        return null;
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
