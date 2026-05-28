using System.Text;

using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class DebugPingPacket : IRakNetPacket
{
    public PacketType Type => PacketType.DebugPing;

    public ulong Timestamp { get; set; } = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public void ReadFrom(Stream stream)
    {
        if (stream.Length - stream.Position < 8) return;

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        Timestamp = reader.ReadUInt64();
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(Timestamp);
    }
}
