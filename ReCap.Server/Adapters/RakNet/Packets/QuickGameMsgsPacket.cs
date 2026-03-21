using System.IO;
using System.Text;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class QuickGameMsgsPacket : IRakNetPacket
{
    public PacketType Type => PacketType.QuickGameMsgs;

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        
        // C++ Server:
        // This packet contains setup messages for Quick Game
        // For now, sending an empty payload or minimal payload
        // C++ implementation in QuickGameMsgs just writes the Packet ID in many cases,
        // or a default value.
        // I will write a 0 byte to signify no extended messages, or just nothing.
        // Let's check Darkspore decomp: QuickGameMsgs usually has a message type byte.
        // Send a 0 type (None).
        writer.Write((byte)0);
    }
}
