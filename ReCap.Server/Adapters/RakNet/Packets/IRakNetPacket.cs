namespace ReCap.Server.Adapters.RakNet.Packets;

public interface IRakNetPacket
{
    PacketType Type { get; }

    void ReadFrom(Stream stream);
    void WriteTo(Stream stream);
}
