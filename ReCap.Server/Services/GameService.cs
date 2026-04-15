using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Models;

namespace ReCap.Server.Services;

public class GameService : IGameHandler
{
    public Dictionary<ulong, Game> Games { get; } = new();           // GameId -> Game
    public Dictionary<ulong, ulong> GameAssigments { get; } = new(); // UserId -> GameId

    public ulong GameCounter { get; private set; } = 0x0080000000000001;

    public AssetDatabase? Assets { get; set; }

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
        var game = new Game(GameCounter++, GameType.Matched, Assets);
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