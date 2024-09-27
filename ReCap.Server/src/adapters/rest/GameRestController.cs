using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;
using LoggerUtil;

namespace HttpServer;

[RestController(Value="/game/api", ContentType="text/xml")]
public class GameRestController
{
    private AccountMapper accountMapper;
    private AccountService accountService;
    private CreatureMapper creatureMapper;
    private CreatureService creatureService;
    private DeckMapper deckMapper;
    private DeckService deckService;
    private CreaturePartMapper creaturePartMapper;
    private CreaturePartService creaturePartService;

    public GameRestController(SqliteConfig newSqliteConfig) {
        accountMapper = new AccountMapper();
        accountService = new AccountService(newSqliteConfig);
        creatureMapper = new CreatureMapper();
        creatureService = new CreatureService(newSqliteConfig);
        deckMapper = new DeckMapper();
        deckService = new DeckService(newSqliteConfig);
        creaturePartMapper = new CreaturePartMapper();
        creaturePartService = new CreaturePartService(newSqliteConfig);
    }

    [RequestMapping(Name="api.account.auth")]
    public byte[] loginPlayerAccount(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string key = parameters["key"]; // "{auth_token}::0" (example: "1::0")
        string[] keyParts = key.Split("::");
        string authToken = keyParts[0];
        AccountModel account = accountService.getAccountByAuthToken(authToken);

        int newPlayerProgress = Convert.ToInt32(parameters.GetValueOrDefault("new_player_progress", "0"));
        if (newPlayerProgress != 0) {
            account.newPlayerProgress = newPlayerProgress;
        }

        bool includeCreatures = Convert.ToBoolean(parameters.GetValueOrDefault("include_creatures", "false"));
        bool includeDecks = Convert.ToBoolean(parameters.GetValueOrDefault("include_decks", "false"));
        bool includeFeed = Convert.ToBoolean(parameters.GetValueOrDefault("include_feed", "false"));
        bool includeSettings = Convert.ToBoolean(parameters.GetValueOrDefault("include_settings", "false"));
        bool includeServerTuning = Convert.ToBoolean(parameters.GetValueOrDefault("include_server_tuning", "false"));
        bool includeTokenCookie = Convert.ToBoolean(parameters.GetValueOrDefault("cookie", "false"));

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
            context.Response.Headers.Add("Set-Cookie", $"token={authToken}");
        }

        return XmlUtils.Serialize(response);
    }

    [RequestMapping(Name="api.account.getAccount")]
    public byte[] getPlayerAccount(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        var account = accountService.getAccountByAuthToken(parameters["token"]);

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

    [RequestMapping(Name="api.account.logout")]
    public byte[] logoutPlayerAccount(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        accountService.deleteAuthToken(parameters["token"]);

        var response = new ResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlUtils.Serialize(response);
    }

    [RequestMapping(Name="api.account.searchAccounts")]
    public byte[] searchPlayerAccounts(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [RequestMapping(Name="api.account.setSettings")]
    public byte[] setPlayerAccountSettings(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string settingsStr = parameters["settings"]; // Example: Key1,Val1;Key2,Val2;Key3,Val3;

        var account = accountService.getAccountByAuthToken(parameters["token"]);

        string[] settings = settingsStr.Split(";");
        foreach (string setting in settings) {
            if (setting.Length > 0) {
                string[] keyAndValue = setting.Split(",");
                account.settings[keyAndValue[0]] = keyAndValue[1];
            }
        }

        accountService.updateAccount(account);

        var response = new ResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlUtils.Serialize(response);
    }

    [RequestMapping(Name="api.account.unlock")]
    public byte[] unlockPlayerAccount(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [RequestMapping(Name="api.account.setNewPlayerStats")]
    public byte[] setNewPlayerStats(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [RequestMapping(Name="api.creature.getCreature")]
    public byte[] getCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [RequestMapping(Name="api.creature.getTemplate")]
    public byte[] getTemplate(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        int templateId = Convert.ToInt32(parameters["id"]);
        bool includeAbilities = parameters["include_abilities"] == "true";

        var account = accountService.getAccountByAuthToken(parameters["token"]);

        var template = creatureService.getCreatureTemplateById((ulong)templateId);

        // TODO: Implement getTemplate returning GetCreatureTemplateResponseContract

        return null;
    }

    [RequestMapping(Name="api.creature.resetCreature")]
    public byte[] resetCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        int creatureId = Convert.ToInt32(parameters["id"]);

        var account = accountService.getAccountByAuthToken(parameters["token"]);

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

    [RequestMapping(Name="api.creature.unlockCreature")]
    public byte[] unlockCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        ulong templateId = (ulong)Convert.ToInt32(parameters["template_id"]);
        var account = accountService.getAccountByAuthToken(parameters["token"]);
        var creature = creatureService.addCreature(account, creatureService.getCreatureTemplateById(templateId));
        accountService.updateAccount(account);

        var response = new UnlockCreatureResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1,
            CreatureID = (ulong)creature.ID
        };

        return XmlUtils.Serialize(response);
    }

    [RequestMapping(Name="api.creature.updateCreature")]
    public byte[] updateCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string host = ServerConfig.GetHost();

        ulong creatureId = (ulong)Convert.ToInt32(parameters["id"]);
        int creatureVersion = Convert.ToInt32(parameters["version"]);

        ulong cost = (ulong)Convert.ToInt32(parameters["cost"]);
        double gearScore = Convert.ToDouble(parameters["gear"]);
        double itemPoints = Convert.ToDouble(parameters["points"]);
        List<ulong> partsList = parameters["parts"].Split(",").ToList().Select(partId => (ulong)Convert.ToInt32(partId)).ToList();

         // STR,14,3;DEX,23,5;MIND,13,0;HLTH,200,107;MANA,100,13;PDEF,150,168;EDEF,50,78;CRTR,100,112
        List<CreatureModelStat> stats = parameters["stats"].Split(";").ToList().Select(stat => {
            var statVals = stat.Split(",");
            return new CreatureModelStat{
                statName = statVals[0],
                maxValue = Convert.ToInt32(statVals[1]),
                currentValue = Convert.ToInt32(statVals[2])
            };
        }).ToList();

        // 868969257!minDamage,4;868969257!maxDamage,12;4022963036!percent,50;1137096183!minDamage,24;1137096183!maxDamage,36;1137096183!stunDuration,3;3492557026!minSecondaryDamage,8;3492557026!maxSecondaryDamage,20;3492557026!minDamage,24;3492557026!maxDamage,40;3492557026!radius,4;2779439490!numOrbs,6;2779439490!minDamage,16;2779439490!maxDamage,40;2779439490!deflectionIncrease,100
        List<CreatureModelAbilityStat> abilityStats = parameters["stats_ability_keyvalues"].Split(";").ToList().Select(stat => {
            var statVals1 = stat.Split("!");
            var statVals2 = statVals1[1].Split(",");
            return new CreatureModelAbilityStat{
                key = statVals1[0],
                token = statVals2[0],
                value = statVals2[1]
            };
        }).ToList();

        string largePngBase64 = parameters["large"];
        string largeCrc = parameters["large_crc"];
        string thumbPngBase64 = parameters["thumb"];
        string thumbCrc = parameters["thumb_crc"];

        var account = accountService.getAccountByAuthToken(parameters["token"]);
        var creature = creatureService.getCreatureById(creatureId);
        if (creature.AccountID != account.Id) {
            throw new ForbiddenOperationException("Creature does not belong to this account");
        }

        creature.Version = creatureVersion;

        creature.Cost = cost;
        creature.GearScore = gearScore;
        creature.ItemPoints = itemPoints;

        creature.Parts = partsList;
        creature.Stats = stats;
        creature.AbilityStats = abilityStats;

        creature.LargePngUrl = $"http://{host}/recap/api?method=api.game.getCreatureLargePng&id={creatureId}";
        creature.LargePngBase64 = largePngBase64;
        creature.LargeCrc = largeCrc;

        creature.ThumbPngUrl = $"http://{host}/recap/api?method=api.game.getCreatureThumbPng&id={creatureId}";
        creature.ThumbPngBase64 = thumbPngBase64;
        creature.ThumbCrc = thumbCrc;

        creatureService.updateCreature(creature);

        var response = new ResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlUtils.Serialize(response);
    }

    [RequestMapping(Name="api.deck.updateDecks")]
    public byte[] updateDecks(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [RequestMapping(Name="api.game.exitGame")]
    public byte[] exitGame(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [RequestMapping(Name="api.game.getGame")]
    public byte[] getGame(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [RequestMapping(Name="api.game.getRandomGame")]
    public byte[] getRandomGame(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [RequestMapping(Name="api.inventory.getPartList")]
    public byte[] getPartList(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        // parameters["filter"] (eg.: "market_status_full-owned;")

        // TODO: count variable currently isn't being used
        int count = Convert.ToInt32(parameters.GetValueOrDefault("count", "100000"));

        var account = accountService.getAccountByAuthToken(parameters["token"]);

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

    [RequestMapping(Name="api.inventory.getPartOfferList")]
    public byte[] getPartOfferList(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string authToken = parameters["token"];

        // TODO: Implement getPartOfferList

        var response = new PartListResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1,
            Parts = new List<CreaturePartContract>()
        };

        return XmlUtils.Serialize(response);

    }

    [RequestMapping(Name="api.inventory.updatePartStatus")]
    public byte[] updatePartStatus(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string[] partIds = parameters["part_id"].Split(",");
        string[] statuses = parameters["status"].Split(",");

        int len = partIds.Length < statuses.Length ? partIds.Length : statuses.Length;
		List<CreaturePartModel> creatureParts = new List<CreaturePartModel>();
		if (len > 0) {
			for (int i = 0; i < len; i++) {
				ulong partId = (ulong)Convert.ToInt32(partIds[i]);
				int status = Convert.ToInt32(statuses[i]);

				var part = creaturePartService.getCreaturePartById(partId);
				if (part != null) {
					part.Status = status;
					creatureParts.Add(part);
				}
			}
			creaturePartService.updateCreatureParts(creatureParts);
		}

        var response = new ResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlUtils.Serialize(response);
    }

    [RequestMapping(Name="api.inventory.vendorParts")]
    public byte[] getVendorParts(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        var account = accountService.getAccountByAuthToken(parameters["token"]);

        List<CreaturePartModel> creatureParts = new List<CreaturePartModel>();
        string[] transactions = parameters["transactions"].Split(";"); // eg. w1
        foreach (string transaction in transactions) {
            char type = transaction[0];
            ulong partId = (ulong)Convert.ToInt32(transaction[1]);
            CreaturePartModel part = null;
            
            if (type == 's') { // sell item
                part = creaturePartService.getCreaturePartById(partId);
                creaturePartService.deleteCreaturePart(part);

                account.dna += part.Cost;
                part = null;
            }
            else if (type == 'f') { // turn item into detail/flair
                part = creaturePartService.getCreaturePartById(partId);
                part.IsFlair = true;
            }
            else if (type == 'w') { // buy weapon
                // TODO: Implement buying weapon
            }
            else if (type == 'p') { // parts?
                // TODO: Implement parts
            }
            else if (type == 'b') { // buyback?
                // TODO: Implement buyback
            }
            else {
                Logger.info($"Unknown transaction: {transaction}");
                // TODO: check for more later
            }

            if (part != null) {
                creatureParts.Add(part);
            }
        }

        accountService.updateAccount(account);
        creaturePartService.updateCreatureParts(creatureParts);

        var response = new PartListResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1,
            Parts = creatureParts.Select(creaturePart => creaturePartMapper.toContract(creaturePart)).ToList()
        };

        // utils::xml_add_text_node(docResponse, "dna", user->get_account().dna);

        return XmlUtils.Serialize(response);
    }

    [RequestMapping(Name="api.leaderboard.getLeaderboard")]
    public byte[] getLeaderboard(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [RequestMapping(Name="api.status.getBroadcastList")]
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

    [RequestMapping(Name="api.status.getStatus")]
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
