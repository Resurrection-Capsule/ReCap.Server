namespace ReCap.Server.Adapters.RakNet.Packets;

public class DebugPingPacket : IRakNetPacket
{
    public PacketType Type => PacketType.DebugPing;

    public void ReadFrom(Stream stream)
    {
    }

    public void WriteTo(Stream stream)
    {
    }
}
