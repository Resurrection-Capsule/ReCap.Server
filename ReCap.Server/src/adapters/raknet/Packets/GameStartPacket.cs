using System.Text;

namespace ReCap.RakNet.Packets;

public class GameStartPacket : IRakNetPacket
{
    public PacketType Type => PacketType.GameStart;
    public byte Unk1 { get; set; }

    public GameStartPacket(byte unk1) => Unk1 = unk1;
    public GameStartPacket() { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(Unk1);
    }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        Unk1 = reader.ReadByte();
    }
}