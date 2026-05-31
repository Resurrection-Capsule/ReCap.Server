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

    // Resolve the player's chosen squad (1-based squadId) into the creatures they own
    // and saved in that deck slot. Mirrors C++ Player::SetSquad. Falls back to the
    // account's first owned creatures if the deck slot is empty, so the squad is always
    // real data — never the old hardcoded nouns.
    public IReadOnlyList<SquadCreature> ResolveSquad(AccountModel account, int squadId)
    {
        if (Decks is null || Creatures is null)
            return Array.Empty<SquadCreature>();

        var deck = Decks.getDecksByAccount(account).FirstOrDefault(d => d.Slot == squadId);
        var creatureIds = deck?.CreatureIds ?? new List<ulong>();

        var resolved = creatureIds
            .Select(id => Creatures.getCreatureById(id))
            .Where(c => c is not null)
            .ToList();

        if (resolved.Count == 0)
            resolved = Creatures.getCreaturesByAccount(account).Take(3).ToList();

        return resolved.Select(ToSquadCreature).ToList();
    }

    private SquadCreature ToSquadCreature(CreatureModel creature)
    {
        var noun = (uint)creature.TemplateID;

        float maxHealth = 200f;
        float maxMana = 200f;
        var attrs = Assets?.ResolveClassAttributesForCreature(noun);
        if (attrs is not null)
        {
            var h = attrs.FindByName("maxHealth").AsFloat();
            var m = attrs.FindByName("maxMana").AsFloat();
            if (h > 0f) maxHealth = h;
            if (m > 0f) maxMana = m;
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
            MaxMana: maxMana);
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