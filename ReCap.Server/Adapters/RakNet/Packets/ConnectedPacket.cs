namespace ReCap.Server.Adapters.RakNet.Packets;

public class ConnectedPacket : IRakNetPacket
{
    public PacketType Type => PacketType.Connected;

    public void ReadFrom(Stream stream)
    {
    }

    public void WriteTo(Stream stream)
    {
    }
}
