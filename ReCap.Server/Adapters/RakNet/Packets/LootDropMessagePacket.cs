using System.IO;
using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// LootDropMessage (0xCB) — client→server. Layout UNKNOWN: C++ OnLootDropMessage
// (Server.cpp:1093-1134) is a debug stub (reads nothing, hexdumps; its 6×u32 guess is
// commented out). v1 captures the raw body for observability — map it via Ghidra/capture
// when the client actually emits it (loot phase).
public class LootDropMessagePacket : IRakNetPacket
{
    public PacketType Type => PacketType.LootDropMessage;

    public byte[] RawData { get; set; } = Array.Empty<byte>();

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var remaining = stream.Length - stream.Position;
        if (remaining > 0)
            RawData = reader.ReadBytes((int)remaining);
    }

    public void WriteTo(Stream stream) { /* server never sends this packet */ }
}
