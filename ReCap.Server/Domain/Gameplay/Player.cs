using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Server.Domain.Gameplay;

public class Player(ulong id, byte slot) : BasePlayer(id, slot)
{
    public override bool IsBot => false;

    public RakNetClient? Client { get; set; }

    public uint GameStatus { get; set; }
    public float GameStatusProgress { get; set; }

    public LabsPlayerData? PlayerData { get; set; }

    public ushort UpdateBits { get; private set; }

    public void SetUpdateBits(ushort bits)
    {
        UpdateBits |= bits;
    }

    public void ResetUpdateBits()
    {
        UpdateBits = 0;
    }

    public override void Setup()
    {
    }
}
