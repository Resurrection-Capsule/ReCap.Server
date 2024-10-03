using HttpServer;
using ReCap.Gameplay;
using ReCap.Server.Adapters.Blaze.Component.GameManager;

namespace HttpServer;

public class GameService
{
    public Dictionary<ulong, Game> Games { get; } = new();           // GameId -> Game
    public Dictionary<ulong, ulong> GameAssigments { get; } = new(); // UserId -> GameId

    public ulong GameCounter { get; private set; } = 0x0080000000000001;

    public Game CreateGame()
    {
        var game = new Game(GameCounter++, GameType.Matched);

        Games.Add(game.Id, game);

        return game;
    }

    public Game GetGameByPlayer(AccountModel account) => GetGame(GameAssigments.FirstOrDefault(g => g.Key == account.Id).Value);

    public Game GetGame(ulong id) => Games.FirstOrDefault(g => g.Key == id).Value;

    public List<Game> GetAllGames() => Games.Values.ToList();

    public bool AddPlayerToGame(ulong gameId, AccountModel account)
    {
        if (GameAssigments.ContainsKey(account.Id))
            return false;

        GameAssigments.Add(account.Id, gameId);
        GetGame(gameId).AttachPlayer(account);
        return true;
    }
}