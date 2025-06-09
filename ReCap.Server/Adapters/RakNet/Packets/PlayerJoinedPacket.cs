using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class PlayerJoinedPacket : IRakNetPacket
{
    public PacketType Type => PacketType.PlayerJoined;

    public byte Slot { get; set; }

    public PlayerJoinedPacket() { }
    public PlayerJoinedPacket(byte slot) => Slot = slot;

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        Slot = reader.ReadByte();
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(Slot);
    }
}
