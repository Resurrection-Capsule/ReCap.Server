using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using ReCap.Server.Adapters.Rest.Contracts;
using ReCap.Server.Adapters.Rest.Contracts.Game;
using ReCap.Server.Adapters.Rest.Contracts.Game.Models;
using ReCap.Server.Config;
using ReCap.Server.Mappers;
using ReCap.Server.Models;
using ReCap.Server.Services;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.Rest.Api;

[RestController(Value="/game/api", ContentType="text/xml")]
public class GameRestController
{
    // server_tuning item-store offer window (seconds). Server-authored tuning; 24h default.
    private const int ItemstoreOfferPeriodSeconds = 24 * 60 * 60;

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
            Version = ServerConfig.GameVersionStr,
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
            // Client parses <settings> generically (each key -> store; "on"/"off"/int),
            // ClientRest::OnAccountResponse settings loop (Ghidra @0x00469190). Emit the
            // modelled toggles so the client has real values instead of an empty block.
            response.Settings = new SettingsContract{
                ShowConfigAlerts = "on",
                Cheat = "off",
                SafeMode = "off"
            };
        }

        if (includeServerTuning) {
            // server_tuning = item-store economy config the client reads to price/refresh the vendor
            // (ClientRest::ParseServerTuningBlock @0x0045f8a0): offer_period = the offer window in
            // SECONDS; current_expiration = unix SECONDS when the current offer set expires; the 7
            // cost multipliers are per-rarity pricing. Server-authored tuning (defaults for now).
            int nowUnix = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            response.ServerTuning = new ServerTuningContract{
                ItemstoreOfferPeriod = ItemstoreOfferPeriodSeconds,
                ItemstoreCurrentExpiration = nowUnix + ItemstoreOfferPeriodSeconds,
                ItemstoreCostMultiplierBasic = 1.0,
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

        var xml = XmlHelper.Serialize(response);
        ReCap.Server.Util.Logging.Log.Rest.Debug($"[AUTH-DUMP] creatures={response.Creatures?.Count ?? -1} bytes={xml.Length}\n{System.Text.Encoding.UTF8.GetString(xml)}");
        return xml;
    }

    [RequestMapping(Name="api.account.getAccount")]
    public byte[]? getPlayerAccount(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        AccountModel account;
        try
        {
            account = accountService.getAccountByAuthToken(parameters["token"]);
        }
        catch (ForbiddenOperationException)
        {
            // C++ game_account_getAccount bails with an empty body on an unknown/stale token
            // (API.cpp:1423-1428, literal "// Send some error?" TODO). A stale cookie after a
            // server restart must not explode into a 500 through the reflection dispatcher.
            ReCap.Server.Util.Logging.Log.Rest.Warn("getAccount: unknown auth token — empty response (C++ parity)");
            return Array.Empty<byte>();
        }

        string httpMethod = context.Request.HttpMethod;
        if (httpMethod == "GET")
        {
            var creatures = creatureService.getCreaturesByAccount(account);
            var response = new GetAccountResponseContract{
                Stat = "ok",
                Version = ServerConfig.GameVersionStr,
                Timestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds * 1000),
                ExecTime = 1,
                Account = accountMapper.toContract(account),
                Decks = deckService.getDecksByAccount(account).Select(deck => deckMapper.toContract(deck, creatures)).ToList()
            };
            return XmlHelper.Serialize(response);
        }
        if (httpMethod == "POST")
        {
            bool includeCreatures = Convert.ToBoolean(parameters.GetValueOrDefault("include_creatures"));
            bool includeDecks = Convert.ToBoolean(parameters.GetValueOrDefault("include_decks"));
            bool includeFeed = Convert.ToBoolean(parameters.GetValueOrDefault("include_feed"));
            bool includeStats = Convert.ToBoolean(parameters.GetValueOrDefault("include_stats"));
            
            if (includeCreatures || includeDecks || includeFeed || includeStats) {
                var creatures = creatureService.getCreaturesByAccount(account);

                var response = new PostAccountResponseContract{
                    Stat = "ok",
                    Version = ServerConfig.GameVersionStr,
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

                return XmlHelper.Serialize(response);
            }
            else {
                var response = new PostAccountResponseContract{
                    Stat = "ok",
                    Version = ServerConfig.GameVersionStr,
                    Timestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds * 1000),
                    ExecTime = 1,
                    BlazeID = account.Id,
                    Name = account.Username,
                    GrantOnlineAccess = (account.grantOnlineAccess ?? false) ? 1 : 0,
                    CashoutBonusTime = account.cashoutBonusTime,
                    Account = accountMapper.toContract(account)
                };
                return XmlHelper.Serialize(response);
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
            Version = ServerConfig.GameVersionStr,
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlHelper.Serialize(response);
    }

    [RequestMapping(Name="api.account.searchAccounts")]
    public byte[]? searchPlayerAccounts(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [RequestMapping(Name="api.account.setSettings")]
    public byte[] setPlayerAccountSettings(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string settingsStr = parameters["settings"]; // Example: Key1,Val1;Key2,Val2;Key3,Val3;

        var account = accountService.getAccountByAuthToken(parameters["token"]);

        account.settings ??= new Dictionary<string,string>();
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
            Version = ServerConfig.GameVersionStr,
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlHelper.Serialize(response);
    }

    [RequestMapping(Name="api.account.unlock")]
    public byte[]? unlockPlayerAccount(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        // Client ClientRest::ParseUnlockResponse re-parses the <account> block after an unlock.
        // unlock_id is an upgrade unlock; per-account unlock persistence is DEFERRED (single-player
        // runs unlock-all/isTest), so acknowledge and return the current account block.
        var account = accountService.getAccountByAuthToken(parameters["token"]);
        var response = new AuthResponseContract{
            Stat = "ok",
            Version = ServerConfig.GameVersionStr,
            Timestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds * 1000),
            ExecTime = 1,
            Account = accountMapper.toContract(account)
        };
        return XmlHelper.Serialize(response);
    }

    [RequestMapping(Name="api.account.setNewPlayerStats")]
    public byte[] setNewPlayerStats(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        // Darkspore sends this on new accounts expecting an auth response. Redirect it to auth handler.
        if (parameters.TryGetValue("token", out var token)) {
            parameters["key"] = $"{token}::0";
        }
        return loginPlayerAccount(context, parameters);
    }

    [RequestMapping(Name="api.creature.getCreature")]
    public byte[] getCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        ulong creatureId = (ulong)Convert.ToInt64(parameters["id"]);
        bool includeAbilities = parameters["include_abilities"] == "true";
        bool includeParts = parameters["include_parts"] == "true";

        var account = accountService.getAccountByAuthToken(parameters["token"]);

        var creature = creatureService.getCreatureById(creatureId)
            ?? throw new ForbiddenOperationException("Creature not found");
        if (creature.AccountID != account.Id) {
            throw new ForbiddenOperationException("Creature does not belong to this account");
        }

        var template = creatureService.getCreatureTemplateById(creature.TemplateID)
            ?? throw new ForbiddenOperationException("Creature template not found");

        var response = creatureMapper.toGetCreatureContract(template, creature, includeAbilities, includeParts);
        return XmlHelper.Serialize(response);
    }

    [RequestMapping(Name="api.creature.getTemplate")]
    public byte[]? getTemplate(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        ulong templateId = (ulong)Convert.ToInt64(parameters["id"]);
        bool includeAbilities = parameters["include_abilities"] == "true";

        var account = accountService.getAccountByAuthToken(parameters["token"]);

        var template = creatureService.getCreatureTemplateById(templateId)
            ?? throw new ForbiddenOperationException("Creature template not found");

        var response = creatureMapper.toGetCreatureTemplateContract(template);
        return XmlHelper.Serialize(response);
    }

    [RequestMapping(Name="api.creature.resetCreature")]
    public byte[] resetCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        ulong creatureId = (ulong)Convert.ToInt64(parameters["id"]);

        var account = accountService.getAccountByAuthToken(parameters["token"]);

        var creature = creatureService.getCreatureById(creatureId)
            ?? throw new ForbiddenOperationException("Creature not found");
        if (creature.AccountID != account.Id) {
            throw new ForbiddenOperationException("Creature does not belong to this account");
        }

        // TODO: Actually reset creature

        var response = new ResponseContract{
            Stat = "ok",
            Version = ServerConfig.GameVersionStr,
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlHelper.Serialize(response);
    }

    [RequestMapping(Name="api.creature.unlockCreature")]
    public byte[] unlockCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        ulong templateId = (ulong)Convert.ToInt64(parameters["template_id"]);
        var account = accountService.getAccountByAuthToken(parameters["token"]);
        var template = creatureService.getCreatureTemplateById(templateId)
            ?? throw new ForbiddenOperationException("Creature template not found");
        var creature = creatureService.addCreature(account, template);
        accountService.updateAccount(account);

        var response = new UnlockCreatureResponseContract{
            Stat = "ok",
            Version = ServerConfig.GameVersionStr,
            Timestamp = 1,
            ExecTime = 1,
            CreatureID = (ulong)creature.ID
        };

        return XmlHelper.Serialize(response);
    }

    [RequestMapping(Name="api.creature.updateCreature")]
    public byte[] updateCreature(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string host = ServerConfig.HostName;

        ulong creatureId = (ulong)Convert.ToInt64(parameters["id"]);
        int creatureVersion = Convert.ToInt32(parameters["version"]);

        ulong cost = (ulong)Convert.ToInt64(parameters["cost"]);
        double gearScore = Convert.ToDouble(parameters["gear"]);
        double itemPoints = Convert.ToDouble(parameters["points"]);
        string partsList = parameters["parts"];

        string stats = parameters["stats"];
        string abilityStats = parameters["stats_ability_keyvalues"];

        string largePngBase64 = parameters["large"];
        string largeCrc = parameters["large_crc"];
        string thumbPngBase64 = parameters["thumb"];
        string thumbCrc = parameters["thumb_crc"];

        var account = accountService.getAccountByAuthToken(parameters["token"]);
        var creature = creatureService.getCreatureById(creatureId)
            ?? throw new ForbiddenOperationException("Creature not found");
        if (creature.AccountID != account.Id) {
            throw new ForbiddenOperationException("Creature does not belong to this account");
        }

        creature.Version = creatureVersion;

        creature.Cost = cost;
        creature.GearScore = gearScore;
        creature.ItemPoints = itemPoints;

        creature.setPartsWithString(partsList);
        creature.setStatsWithString(stats);
        creature.setAbilityStatsWithString(abilityStats);

        creature.LargePngUrl = $"http://{host}/recap/api?method=api.game.getCreatureLargePng&id={creatureId}";
        creature.LargePngBase64 = largePngBase64;
        creature.LargeCrc = largeCrc;

        creature.ThumbPngUrl = $"http://{host}/recap/api?method=api.game.getCreatureThumbPng&id={creatureId}";
        creature.ThumbPngBase64 = thumbPngBase64;
        creature.ThumbCrc = thumbCrc;

        creatureService.updateCreature(creature);

        var response = new ResponseContract{
            Stat = "ok",
            Version = ServerConfig.GameVersionStr,
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlHelper.Serialize(response);
    }

    [RequestMapping(Name="api.deck.updateDecks")]
    public byte[] updateDecks(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        // C++ game_deck_updateDecks (API.cpp:1866-1881): pve_active_slot + pve_creatures
        // (CSV; the client sends all 9 slot values, zeros for empty), idem pvp.
        var account = accountService.getAccountByAuthToken(parameters["token"]);
        var ownedIds = creatureService.getCreaturesByAccount(account).Select(c => c.ID).ToHashSet();

        ApplyDeckUpdate(account.Id, ownedIds, parameters, "pve_active_slot", "pve_creatures", "pve");
        ApplyDeckUpdate(account.Id, ownedIds, parameters, "pvp_active_slot", "pvp_creatures", "pvp");

        return XmlHelper.Serialize(new Contracts.ResponseContract { Stat = "ok", Code = 200, Result = 1 });
    }

    private void ApplyDeckUpdate(ulong accountId, IReadOnlySet<ulong> ownedIds,
                                 Dictionary<string,string> parameters,
                                 string slotKey, string creaturesKey, string category)
    {
        if (!parameters.TryGetValue(slotKey, out var slotStr) || !int.TryParse(slotStr, out var slot))
            return;

        var requestedIds = parameters.GetValueOrDefault(creaturesKey, "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => (ulong)Convert.ToInt64(s.Trim()))
            .ToList();

        deckService.updateDeck(accountId, slot, requestedIds, ownedIds, category);
    }

    [RequestMapping(Name="api.game.exitGame")]
    public byte[]? exitGame(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        // Client response handler is a no-op (ignores the body); return a valid ack, not an empty body.
        return XmlHelper.Serialize(new ResponseContract{
            Stat = "ok", Version = ServerConfig.GameVersionStr, Timestamp = 1, ExecTime = 1
        });
    }

    [RequestMapping(Name="api.game.getGame")]
    public byte[]? getGame(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [RequestMapping(Name="api.game.getRandomGame")]
    public byte[]? getRandomGame(HttpListenerContext context, Dictionary<string,string> parameters)
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
            Version = ServerConfig.GameVersionStr,
            Timestamp = 1,
            ExecTime = 1,
            Parts = creatureParts.Select(creaturePart => creaturePartMapper.toContract(creaturePart)).ToList()
        };

        return XmlHelper.Serialize(response);
    }

    [RequestMapping(Name="api.inventory.getPartOfferList")]
    public byte[] getPartOfferList(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        // Store offer = the full part-template pool (client: ClientRest::ParsePartOfferListResponse
        // reads timestamp + expires + parts). Per-rarity display pricing is applied client-side from
        // server_tuning.itemstore_cost_multiplier_*; the server emits each part's base cost. expires
        // aligns to the same 24h window as server_tuning.itemstore_current_expiration.
        long expires = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds + ItemstoreOfferPeriodSeconds;

        var offer = creaturePartService.getAllTemplates().Select(template => {
            var part = creaturePartMapper.toCreaturePartModel(template, false);
            part.ID = template.rigblockAssetId; // stable per-offer id (template key)
            return creaturePartMapper.toContract(part);
        }).ToList();

        var response = new PartListResponseContract{
            Stat = "ok",
            Version = ServerConfig.GameVersionStr,
            Timestamp = 1,
            ExecTime = 1,
            Expires = expires,
            Parts = offer
        };

        return XmlHelper.Serialize(response);
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
				ulong partId = (ulong)Convert.ToInt64(partIds[i]);
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
            Version = ServerConfig.GameVersionStr,
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlHelper.Serialize(response);
    }

    [RequestMapping(Name="api.inventory.vendorParts")]
    public byte[] getVendorParts(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        var account = accountService.getAccountByAuthToken(parameters["token"]);

        // transactions = ';'-delimited list of <typeChar><partId> (C++ API.cpp:1097 explode ';';
        // byte 0 = type, rest = id). Types: s=sell f=flair (implemented) / b,w,p=buy (no-op: this
        // account already owns the full part pool, so the store is sell+flair only per design).
        var touched = new List<CreaturePartModel>();
        foreach (string transaction in parameters["transactions"].Split(";", StringSplitOptions.RemoveEmptyEntries)) {
            char type = transaction[0];
            if (!ulong.TryParse(transaction.AsSpan(1), out ulong partId)) {
                ReCap.Server.Util.Logging.Log.Rest.Info($"vendorParts: bad transaction '{transaction}'");
                continue;
            }

            switch (type) {
                case 's': { // sell — remove part, refund its cost as DNA
                    var part = creaturePartService.getCreaturePartById(partId);
                    if (part != null && part.AccountId == account.Id) {
                        creaturePartService.deleteCreaturePart(part);
                        account.dna += part.Cost;
                    }
                    break;
                }
                case 'f': { // flair — convert part to a cosmetic detail
                    var part = creaturePartService.getCreaturePartById(partId);
                    if (part != null && part.AccountId == account.Id && !part.IsFlair) {
                        part.IsFlair = true;
                        touched.Add(part);
                    }
                    break;
                }
                case 'b': case 'w': case 'p': // buy/weapon/parts — no-op (full pool already owned)
                    break;
                default:
                    ReCap.Server.Util.Logging.Log.Rest.Info($"vendorParts: unknown type '{type}' in '{transaction}'");
                    break;
            }
        }

        if (touched.Count > 0) {
            creaturePartService.updateCreatureParts(touched);
        }
        accountService.updateAccount(account);

        // Response = updated inventory + new DNA balance (ClientRest::ParseVendorPartsResponse: parts + dna).
        var inventory = creaturePartService.getCreaturePartsByAccount(account)
            .Where(part => part.CreatureId is null).ToList();

        var response = new PartListResponseContract{
            Stat = "ok",
            Version = ServerConfig.GameVersionStr,
            Timestamp = 1,
            ExecTime = 1,
            Parts = inventory.Select(part => creaturePartMapper.toContract(part)).ToList(),
            Dna = account.dna
        };

        return XmlHelper.Serialize(response);
    }

    [RequestMapping(Name="api.leaderboard.getLeaderboard")]
    public byte[]? getLeaderboard(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        return null;
    }

    [RequestMapping(Name="api.status.getBroadcastList")]
    public byte[] getBroadcastList(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        var response = new StatusResponseContract{
            Stat = "ok",
            Version = ServerConfig.GameVersionStr,
            Timestamp = 1,
            ExecTime = 1,
            Broadcasts = BroadcastService.getBroadcastList()
        };

        return XmlHelper.Serialize(response);
    }

    [RequestMapping(Name="api.status.getStatus")]
    public byte[] getStatus(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        string? darksporeVersion = parameters.GetValueOrDefault("build");
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

        return XmlHelper.Serialize(response);
    }
}
