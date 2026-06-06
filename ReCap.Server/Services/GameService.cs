using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Models;
using ReCap.Server.Services.Assets;

namespace ReCap.Server.Services;

public class GameService : IGameHandler
{
    public Dictionary<ulong, Game> Games { get; } = new();           // GameId -> Game
    public Dictionary<ulong, ulong> GameAssigments { get; } = new(); // UserId -> GameId

    public ulong GameCounter { get; private set; } = 0x0080000000000001;

    public AssetDatabase? Assets { get; set; }
    public DeckService? Decks { get; set; }
    public CreatureService? Creatures { get; set; }
    public Scripting.ScriptEngine? Scripts { get; set; }

    // Mirrors C++ PrepareGameStart + Player::SetSquad (Server.cpp:1210-1241,
    // Player.cpp:267-313): the squad is EXACTLY the persisted deck. Missing deck or
    // empty slots resolve to an empty/partial squad — the server never injects
    // creatures (spec 2026-06-04-deck-system-design). Empty slots become noun=0
    // characters downstream in FillSquadCharacters, matching C++ null characters.
    public IReadOnlyList<SquadCreature> ResolveSquad(AccountModel account, int squadId)
    {
        if (Decks is null || Creatures is null)
            return Array.Empty<SquadCreature>();

        var deck = Decks.getDecksByAccount(account).FirstOrDefault(d => d.Slot == squadId);
        if (deck is null)
            return Array.Empty<SquadCreature>();

        return deck.CreatureIds
            .Where(id => id != 0)
            .Select(id => Creatures.getCreatureById(id))
            .OfType<CreatureModel>()
            .Select(ToSquadCreature)
            .ToList();
    }

    private SquadCreature ToSquadCreature(CreatureModel creature)
    {
        var noun = (uint)creature.TemplateID;

        float maxHealth = 200f;
        float maxMana = 200f;
        // Base stats: C++ Object setup reads ClassAttributes base* fields (Object.cpp:608-662);
        // the working binary ships them in the spawn 0x96 (capture msgs #607/#612/#617).
        float strength = 0f, dexterity = 0f, mind = 0f;
        float physicalDefense = 0f, energyDefense = 0f, criticalRating = 0f;
        float nonCombatSpeed = 0f, combatSpeed = 0f;
        var attrs = Assets?.ResolveClassAttributesForCreature(noun);
        if (attrs is not null)
        {
            var h = attrs.FindByName("maxHealth").AsFloat();
            var m = attrs.FindByName("maxMana").AsFloat();
            if (h > 0f) maxHealth = h;
            if (m > 0f) maxMana = m;
            strength = attrs.FindByName("baseStrength").AsFloat();
            dexterity = attrs.FindByName("baseDexterity").AsFloat();
            mind = attrs.FindByName("baseMind").AsFloat();
            physicalDefense = attrs.FindByName("basePhysicalDefense").AsFloat();
            energyDefense = attrs.FindByName("baseEnergyDefense").AsFloat();
            criticalRating = attrs.FindByName("baseCritical").AsFloat();
            nonCombatSpeed = attrs.FindByName("baseNonCombatSpeed").AsFloat();
            combatSpeed = attrs.FindByName("baseCombatSpeed").AsFloat();
        }

        var gearScore = (float)creature.GearScore;
        var template = Creatures?.getCreatureTemplateById(creature.TemplateID);
        var creatureType = CreatureElement.ToWireType(template?.elementType);

        return new SquadCreature(
            Noun: noun,
            Version: creature.Version,
            CreatureType: creatureType,
            GearScore: gearScore,
            GearScoreFlattened: gearScore,
            MaxHealth: maxHealth,
            MaxMana: maxMana,
            Strength: strength,
            Dexterity: dexterity,
            Mind: mind,
            PhysicalDefense: physicalDefense,
            EnergyDefense: energyDefense,
            CriticalRating: criticalRating,
            NonCombatSpeed: nonCombatSpeed,
            CombatSpeed: combatSpeed);
    }

    // ── IGameHandler ──────────────────────────────────────────────────────────

    IGame? IGameHandler.CreateGame()           => CreateGame();
    IGame? IGameHandler.GetGame(ulong id)      => GetGame(id);

    bool IGameHandler.AddClientToGame(ulong clientId, ulong gameId)
    {
        if (GameAssigments.ContainsKey(clientId)) return false;
        GameAssigments.Add(clientId, gameId);
        return true;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public Game CreateGame()
    {
        var game = new Game(GameCounter++, GameType.Matched, Assets)
        {
            SquadResolver = ResolveSquad
        };
        if (Scripts is not null)
        {
            try
            {
                game.ScriptContext = new Scripting.GameScriptContext(game, Scripts);
            }
            catch (Exception ex)
            {
                Util.Logging.Log.Game.Error($"Game {game.Id} script context boot failed: {ex.Message}");
            }
        }
        Games.Add(game.Id, game);
        return game;
    }

    public Game? GetGameByPlayer(AccountModel account)
    {
        if (!GameAssigments.TryGetValue(account.Id, out var gameId)) return null;
        return GetGame(gameId);
    }

    public Game? GetGame(ulong id)
        => Games.TryGetValue(id, out var g) ? g : null;

    public List<Game> GetAllGames() => Games.Values.ToList();

    public bool AddPlayerToGame(ulong gameId, AccountModel account, byte slot = 0)
    {
        var game = GetGame(gameId);
        if (game is null) return false;

        if (!GameAssigments.ContainsKey(account.Id))
            GameAssigments.Add(account.Id, gameId);

        // Note: the player is actually attached in RakNetServer
        // once they connect and send HelloPlayerRequest.
        return true;
    }
}