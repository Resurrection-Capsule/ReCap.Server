using SharpRakNet.Network;
using SharpRakNet.Protocol.Raknet;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Server.Adapters.RakNet;

public record RakNetClient(RaknetSession Session)
{
    public ulong UserId { get; set; }
    public ulong PlaygroupId { get; set; }
    public Game? Game { get; set; }

    public void SendPacket(IRakNetPacket packet, Reliability reliability = Reliability.ReliableOrdered) => RakNetServer.SendPacket(this, packet, reliability);
}