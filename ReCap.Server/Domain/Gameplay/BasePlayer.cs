namespace ReCap.Server.Domain.Gameplay;

public enum PlayerStatus
{
    None,
    Connecting,
    Connected
}

public abstract class BasePlayer(ulong id, byte slot)
{
    public ulong Id { get; } = id;
    public byte Slot { get; } = slot;
    public PlayerStatus Status { get; protected set; }

    public abstract bool IsBot { get; }

    public abstract void Setup();
}
