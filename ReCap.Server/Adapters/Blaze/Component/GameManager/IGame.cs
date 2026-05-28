namespace ReCap.Server.Adapters.Blaze.Component.GameManager;

public enum GameType
{
    Managed       = 0,
    Matched       = 1,
    Solo          = 2,
    PracticeSolo  = 3,
    PracticeGroup = 4
}

[Flags]
public enum GameFlags
{
    Matched            = 0x0001,
    Custom             = 0x0002,
    Solo               = 0x0004,
    SomeSpectatorStuff = 0x1000
}

public interface IGame
{
    ulong Id { get; }

    bool SetupPlayer(ulong playerId, byte slot);
    void SetupBot(byte slot);
    void SelectLevel(uint levelIndex);
}
