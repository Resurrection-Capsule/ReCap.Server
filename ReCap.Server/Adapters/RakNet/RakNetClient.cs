using RakNexus.Network;
using RakNexus.Protocol;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Server.Adapters.RakNet;

public record RakNetClient(RakNetSession Session)
{
    public ulong UserId { get; set; }
    public ulong PlaygroupId { get; set; }
    public Game? Game { get; set; }

    public void SendPacket(IRakNetPacket packet, PacketReliability reliability = PacketReliability.RELIABLE_ORDERED) => RakNetServer.SendPacket(this, packet, reliability);
}
