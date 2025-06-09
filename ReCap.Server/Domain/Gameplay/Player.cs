using ReCap.Server.Adapters.RakNet;

namespace ReCap.Server.Domain.Gameplay;

public class Player(ulong id, byte slot) : BasePlayer(id, slot)
{
    public override bool IsBot => false;

    public RakNetClient? Client { get; set; }

    public override void Setup()
    {
        
    }
}
