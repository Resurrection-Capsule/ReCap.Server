namespace ReCap.Domain.Gameplay;

using ReCap.RakNetServer;
using ReCap.Domain.Gameplay;
using ReCap.RakNet;

public class Player(ulong id, byte slot) : BasePlayer(id, slot)
{
    public override bool IsBot => false;

    public RakNetClient? Client { get; set; }

    public override void Setup()
    {
        
    }
}
