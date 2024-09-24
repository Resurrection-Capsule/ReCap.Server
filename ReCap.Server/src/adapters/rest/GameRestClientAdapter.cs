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
    private CreaturePartMapper creaturePartMapper;
    private CreaturePartService creaturePartService;

    public GameRestClientAdapter(SqliteConfig newSqliteConfig) {
        accountMapper = new AccountMapper();
        accountService = new AccountService(newSqliteConfig);
        creatureMapper = new CreatureMapper();
        creatureService = new CreatureService(newSqliteConfig);
        deckMapper = new DeckMapper();
        deckService = new DeckService(newSqliteConfig);
        creaturePartMapper = new CreaturePartMapper();
        creaturePartService = new CreaturePartService(newSqliteConfig);
    }

    [ApiMethod(Name="api.account.auth")]
    public byte[] loginPlayerAccount(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        var request = context.Request;

        string authToken = null;
        AccountModel account = null;

        string key = parameters.GetValueOrDefault("key", null);
        if (key != null) {
            // key = "{auth_token}::0" (example: "1::0")
            string[] keyParts = key.Split("::");
            authToken = keyParts[0];
            account = accountService.getAccountByAuthToken(authToken);
        }
        if (account == null) {
            throw new ForbiddenOperationException("Unindentified account");
        }

        int newPlayerProgress = Convert.ToInt32(parameters.GetValueOrDefault("new_player_progress", "0"));
        if (newPlayerProgress != 0) {
            account.newPlayerProgress = newPlayerProgress;
        }

        bool includeCreatures = Convert.ToBoolean(parameters.GetValueOrDefault("include_creatures", null));
        bool includeDecks = Convert.ToBoolean(parameters.GetValueOrDefault("include_decks", null));
        bool includeFeed = Convert.ToBoolean(parameters.GetValueOrDefault("include_feed", null));
        bool includeSettings = Convert.ToBoolean(parameters.GetValueOrDefault("include_settings", null));
        bool includeServerTuning = Convert.ToBoolean(parameters.GetValueOrDefault("include_server_tuning", null));
        bool includeTokenCookie = Convert.ToBoolean(parameters.GetValueOrDefault("cookie", null));

        var creatures = creatureService.getCreaturesByAccount(account);

        var response = new AuthResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds * 1000),
            ExecTime = 1,
            Account = accountMapper.toContract(account)
        };

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
        string authToken = parameters["token"];
        var account = accountService.getAccountByAuthToken(authToken);

        string httpMethod = context.Request.HttpMethod;
        if (httpMethod == "GET")
        {
            var creatures = creatureService.getCreaturesByAccount(account);
            var response = new GetAccountResponseContract{
                Stat = "ok",
                Version = ServerConfig.GetDarksporeVersion(),
                Timestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds * 1000),
                ExecTime = 1,
                Account = accountMapper.toContract(account),
                Decks = deckService.getDecksByAccount(account).Select(deck => deckMapper.toContract(deck, creatures)).ToList()
            };
            return XmlUtils.Serialize(response);
        }
        if (httpMethod == "POST")
        {
            bool includeCreatures = Convert.ToBoolean(parameters.GetValueOrDefault("include_creatures", null));
            bool includeDecks = Convert.ToBoolean(parameters.GetValueOrDefault("include_decks", null));
            bool includeFeed = Convert.ToBoolean(parameters.GetValueOrDefault("include_feed", null));
            bool includeStats = Convert.ToBoolean(parameters.GetValueOrDefault("include_stats", null));
            
            if (includeCreatures || includeDecks || includeFeed || includeStats) {
                var creatures = creatureService.getCreaturesByAccount(account);

                var response = new PostAccountResponseContract{
                    Stat = "ok",
                    Version = ServerConfig.GetDarksporeVersion(),
                    Timestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds * 1000),
                    ExecTime = 1,
                    Account = accountMapper.toContract(account)
                };

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

                if (includeStats) {
                    response.Stats = [new StatContract{
                        Wins = 0
                    }];
                }

                return XmlUtils.Serialize(response);
            }
            else {
                var response = new PostAccountResponseContract{
                    Stat = "ok",
                    Version = ServerConfig.GetDarksporeVersion(),
                    Timestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds * 1000),
                    ExecTime = 1,
                    BlazeID = account.Id,
                    Name = account.Username,
                    GrantOnlineAccess = (account.grantOnlineAccess ?? false) ? 1 : 0,
                    CashoutBonusTime = account.cashoutBonusTime
                };
                return XmlUtils.Serialize(response);
            }
        }
        return null;
    }

    [ApiMethod(Name="api.account.logout")]
    public byte[] logoutPlayerAccount(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string authToken = parameters["token"];

        accountService.deleteAuthToken(authToken);

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
        string authToken = parameters["token"];
        string settings = parameters["settings"]; // Example: Key1,Val1;Key2,Val2;Key3,Val3;

        var response = new ResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlUtils.Serialize(response);
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
        string authToken = parameters["token"];
        int templateId = Convert.ToInt32(parameters["id"]);
        bool includeAbilities = parameters["include_abilities"] == "true";

        var account = accountService.getAccountByAuthToken(authToken);
        if (account == null) {
            throw new ForbiddenOperationException("Unindentified account");
        }

        var template = creatureService.getCreatureTemplateById((ulong)templateId);

        // TODO: Implement getTemplate returning GetCreatureTemplateResponseContract

        return null;
    }

    [ApiMethod(Name="api.creature.resetCreature")]
    public byte[] resetCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string authToken = parameters["token"];
        int creatureId = Convert.ToInt32(parameters["id"]);

        var account = accountService.getAccountByAuthToken(authToken);

        var creature = creatureService.getCreatureById((ulong)creatureId);
        if (creature.AccountID != account.Id) {
            throw new ForbiddenOperationException("Creature does not belong to this account");
        }

        // TODO: Actually reset creature

        var response = new ResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlUtils.Serialize(response);
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
        // parameters["filter"] (eg.: "market_status_full-owned;")
        string authToken = parameters["token"];

        // TODO: count variable currently isn't being used
        int count = Convert.ToInt32(parameters.GetValueOrDefault("count", "100000"));

        var account = accountService.getAccountByAuthToken(authToken);
        var creatureParts = creaturePartService.getCreaturePartsByAccount(account)
            .Where(creaturePart => creaturePart.CreatureId is null).ToList();
        // TODO: Should I list used creatureParts as well?

        var response = new PartListResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1,
            Parts = creatureParts.Select(creaturePart => creaturePartMapper.toContract(creaturePart)).ToList()
        };

        return XmlUtils.Serialize(response);
    }

    [ApiMethod(Name="api.inventory.getPartOfferList")]
    public byte[] getPartOfferList(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string authToken = parameters["token"];

        return null;
    }

    [ApiMethod(Name="api.inventory.updatePartStatus")]
    public byte[] updatePartStatus(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        // TODO: api.inventory.updatePartStatus

        // auto partIds = utils::explode_string(request.uri.parameter("part_id"), ',');
		// auto statuses = utils::explode_string(request.uri.parameter("status"), ',');

		// size_t len = std::min<size_t>(partIds.size(), statuses.size());
		// if (len > 0) {
		// 	for (size_t i = 0; i < len; i++) {
		// 		uint32_t partId = utils::to_number<uint32_t>(partIds[i]);
		// 		uint8_t  status = utils::to_number<uint8_t>(statuses[i]);

		// 		auto part = Repository::UserParts::getById(partId);
		// 		if (part != nullptr) {
		// 			part->SetStatus(status);
		// 		}
		// 	}
		// 	Repository::UserParts::Save();
		// }

        return null;
    }

    [ApiMethod(Name="api.inventory.vendorParts")]
    public byte[] getVendorParts(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string authToken = parameters["token"];
        var account = accountService.getAccountByAuthToken(authToken);

        string[] transactions = parameters["transactions"].Split(";"); // eg. w1
        foreach (string transaction in transactions) {
            char type = transaction[0];
            ulong partId = (ulong)Convert.ToInt32(transaction[1]);
            
            if (type == 's') { // sell item
                var part = creaturePartService.getCreaturePartById(partId);
                creaturePartService.deleteCreaturePart(part);

                account.dna += part.Cost;
                accountService.updateAccount(account);
            }
            else if (type == 'f') { // turn item into detail/flair
                var part = creaturePartService.getCreaturePartById(partId);
                part.IsFlair = true;
                creaturePartService.updateCreaturePart(part);
            }
            else if (type == 'w'){ // buy weapon
                // TODO: Implement buying weapon
            }
            else {
                // logger::info("Transaction: " + transaction);
                // TODO: check for more later
            }
        }

        var creatureParts = creaturePartService.getCreaturePartsByAccount(account)
            .Where(creaturePart => creaturePart.CreatureId is null).ToList();

        var response = new PartListResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1,
            Parts = creatureParts.Select(creaturePart => creaturePartMapper.toContract(creaturePart)).ToList()
        };

        return XmlUtils.Serialize(response);
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
        string darksporeVersion = parameters.GetValueOrDefault("build", null);
        bool includeBroadcasts = parameters["include_broadcasts"] == "true";

        var response = new StatusResponseContract{
            Stat = "ok",
            Version = darksporeVersion,
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
