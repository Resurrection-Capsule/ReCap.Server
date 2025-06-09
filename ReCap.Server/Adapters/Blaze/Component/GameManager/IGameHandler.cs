namespace ReCap.Server.Adapters.Blaze.Component.GameManager;

public interface IGameHandler
{
    IGame? CreateGame();
    IGame? GetGame(ulong id);

    bool AddClientToGame(ulong clientId, ulong gameId);
}
