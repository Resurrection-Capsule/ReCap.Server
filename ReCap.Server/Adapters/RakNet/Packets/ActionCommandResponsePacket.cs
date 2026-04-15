using System.IO;
using System.Text;

using ReCap.Server.Domain.Gameplay.Objects;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class ActionCommandResponsePacket : IRakNetPacket
{
    public PacketType Type => PacketType.ActionCommandResponse;
    public byte ActionType { get; set; }

    public void ReadFrom(Stream stream) {}

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        
        writer.Write((byte)0xFF);
        writer.Write(ActionType);
        writer.Write((byte)0xAA);
        writer.Write((byte)0xBB);
        
        switch (ActionType)
        {
            case 0x08: // Move to position
                stream.Position += 0x34; // 0x34 padding
                break;
            default:
                break;
        }
    }
}
