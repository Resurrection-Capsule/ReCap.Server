using System.Text;

using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class PartyMergeCompletePacket : IRakNetPacket
{
    public PacketType Type => PacketType.PartyMergeComplete;

    public ulong Timestamp { get; set; }

    public PartyMergeCompletePacket()
    {
        Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        Timestamp = reader.ReadUInt64BE();
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.WriteBE(Timestamp);
    }
}
