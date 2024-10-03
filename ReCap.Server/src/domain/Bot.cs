namespace ReCap.Domain.Gameplay;

public class Bot(ulong id, byte slot) : BasePlayer(id, slot)
{
    public override bool IsBot => true;

    public override void Setup()
    {
        Status = PlayerStatus.Connected;
    }
}
