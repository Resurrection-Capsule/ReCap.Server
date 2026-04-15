using System.Text;

using ReCap.Server.Util;

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
        Address = reader.ReadUInt32BE();
        Port = reader.ReadUInt16BE();
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(PlayerType);
        writer.Write(GameplayIndex);
        writer.WriteBE(Address);
        writer.Write(Port);
    }
}
