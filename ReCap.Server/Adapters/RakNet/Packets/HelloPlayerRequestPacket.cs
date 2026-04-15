using System.Text;

using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class HelloPlayerRequestPacket : IRakNetPacket
{
    public PacketType Type => PacketType.HelloPlayerRequest;

    public ulong UserId { get; set; }
    public ulong PlaygroupId { get; set; }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        UserId = reader.ReadUInt64();

        if (stream.Position + 8 <= stream.Length)
            PlaygroupId = reader.ReadUInt64();
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(UserId);
        writer.Write(PlaygroupId);
    }
}
