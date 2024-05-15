using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer;

public class GameRestClientAdapter
{
    private AccountMapper accountMapper;
    private AccountService accountService;
    private CreatureMapper creatureMapper;
    private CreatureService creatureService;
    private DeckMapper deckMapper;
    private DeckService deckService;

    public GameRestClientAdapter(SqliteConfig newSqliteConfig) {
        accountMapper = new AccountMapper();
        accountService = new AccountService(newSqliteConfig);
        creatureMapper = new CreatureMapper();
        creatureService = new CreatureService(newSqliteConfig);
        deckMapper = new DeckMapper();
        deckService = new DeckService(newSqliteConfig);
    }

    [ApiMethod(Name="api.account.auth")]
    public byte[] loginPlayerAccount(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        var request = context.Request;

        string authToken = null;
        Account account = null;

        string key = parameters.GetValueOrDefault("key", null);
        if (key != null) {
            // key = "{auth_token}::0" (example: "1::0")
            string[] keyParts = key.Split("::");
            authToken = keyParts[0];
            account = accountService.getAccountByAuthToken(authToken);

            // TODO: Temporary code
            account.tutorialCompleted = false;
            account.chainProgression = 24;
            account.creatureRewards = 100;
            account.currentGameId = 1;
            account.currentPlaygroupId = 1;
            account.defaultDeckPveId = 1;
            account.defaultDeckPvpId = 1;
            account.level = 100;
            account.dna = 10000000;
            account.newPlayerInventory = 1;
            account.newPlayerProgress = 9500;
            account.cashoutBonusTime = 1;
            account.starLevel = 10;
            account.unlockCatalysts = 1;
            account.unlockDiagonalCatalysts = 1;
            account.unlockFuelTanks = 1;
            account.unlockInventory = 1;
            account.unlockPveDecks = 2;
            account.unlockPvpDecks = 1;
            account.unlockStats = 1;
            account.unlockInventoryIdentify = 2500;
            account.unlockEditorFlairSlots = 1;
            account.upsell = 1;
            account.xp = 10000;
            account.grantAllAccess = true;
            account.grantOnlineAccess = null;
        }
        if (account == null) {
            throw new ForbiddenOperationException("Unindentified account");
        }

        int newPlayerProgress = Convert.ToInt32(parameters.GetValueOrDefault("new_player_progress", "0"));
        if (newPlayerProgress != 0) {
            account.newPlayerProgress = newPlayerProgress;
        }

        // account.Write(docAccount);

        var response = new AuthResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds * 1000),
            ExecTime = 1,
            Account = accountMapper.toContract(account)
        };

        bool includeCreatures = Convert.ToBoolean(parameters.GetValueOrDefault("include_creatures", null));
        bool includeDecks = Convert.ToBoolean(parameters.GetValueOrDefault("include_decks", null));
        bool includeFeed = Convert.ToBoolean(parameters.GetValueOrDefault("include_feed", null));
        bool includeSettings = Convert.ToBoolean(parameters.GetValueOrDefault("include_settings", null));
        bool includeServerTuning = Convert.ToBoolean(parameters.GetValueOrDefault("include_server_tuning", null));
        bool includeTokenCookie = Convert.ToBoolean(parameters.GetValueOrDefault("cookie", null));

        var creatures = creatureService.getCreaturesByAccount(account);

        if (includeCreatures) {
            response.Creatures = creatures.Select(creature => creatureMapper.toContract(creature)).ToList();
        }

        if (includeDecks) {
            response.Decks = deckService.getDecksByAccount(account).Select(deck => deckMapper.toContract(deck, creatures)).ToList();
        }

        if (includeFeed) {
            // TODO: Not implemented
            response.Feed = new FeedContract{
                // Items = []
            };
        }

        if (includeSettings) {
            response.Settings = new SettingsContract{
                // ShowConfigAlerts = true ? "on" : "off",
                // Cheat = true ? "on" : "off",
                // SafeMode = true ? "on" : "off"
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
            string cookieDate = DateTime.UtcNow.AddMinutes(60).ToString("ddd, dd-MMM-yyyy H:mm:ss");
            context.Response.Headers.Add("Set-Cookie", $"token={authToken}");
        }

        return XmlUtils.Serialize(response);
    }

    [ApiMethod(Name="api.account.getAccount")]
    public byte[] getPlayerAccount(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.account.logout")]
    public byte[] logoutPlayerAccount(HttpListenerContext context, Dictionary<string,string> parameters)
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
    public byte[] searchPlayerAccounts(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.account.setSettings")]
    public byte[] setPlayerAccountSettings(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.account.unlock")]
    public byte[] unlockPlayerAccount(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.account.setNewPlayerStats")]
    public byte[] setNewPlayerStats(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.creature.getCreature")]
    public byte[] getCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.creature.getTemplate")]
    public byte[] getTemplate(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.creature.resetCreature")]
    public byte[] resetCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.creature.unlockCreature")]
    public byte[] unlockCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.creature.updateCreature")]
    public byte[] updateCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.deck.updateDecks")]
    public byte[] updateDecks(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.game.exitGame")]
    public byte[] exitGame(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.game.getGame")]
    public byte[] getGame(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.game.getRandomGame")]
    public byte[] getRandomGame(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.inventory.getPartList")]
    public byte[] getPartList(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.inventory.getPartOfferList")]
    public byte[] getPartOfferList(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.inventory.updatePartStatus")]
    public byte[] updatePartStatus(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.inventory.vendorParts")]
    public byte[] getVendorParts(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.leaderboard.getLeaderboard")]
    public byte[] getLeaderboard(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.status.getBroadcastList")]
    public byte[] getBroadcastList(HttpListenerContext context, Dictionary<string,string> parameters)
    {
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
    public byte[] getStatus(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        bool includeBroadcasts = parameters["include_broadcasts"] == "true";

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
