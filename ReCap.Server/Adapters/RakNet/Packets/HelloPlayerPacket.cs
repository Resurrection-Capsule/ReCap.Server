using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class HelloPlayerPacket : IRakNetPacket
{
    public PacketType Type => PacketType.HelloPlayer;

    public byte PlayerType { get; set; }
    public byte GameplayIndex { get; set; }
    public uint Address { get; set; }
    public ushort Port { get; set; }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        PlayerType = reader.ReadByte();
        GameplayIndex = reader.ReadByte();
        Address = reader.ReadUInt32();
        Port = reader.ReadUInt16();
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(PlayerType);
        writer.Write(GameplayIndex);
        writer.Write(Address);
        writer.Write(Port);
    }
}
