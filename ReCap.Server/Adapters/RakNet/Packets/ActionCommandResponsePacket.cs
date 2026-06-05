using System.IO;
using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// ActionCommandResponse (0xA8). Client handler @0x0053cb10 reads exactly 56B and routes
// on ResponseType (byte +1): 1=ability ack, 2=finished, 4=cancel, 8=movement GO
// (client walks to its click-stashed goal via Locomotion::SetGoalPositionWithDistance),
// 0x10=clear. C++ ref: Server.cpp:1659 (type 1 only). CLIENT_MOVEMENT_CONTRACT.md.
public class ActionCommandResponsePacket : IRakNetPacket
{
    private const int BodySize = 56;

    public PacketType Type => PacketType.ActionCommandResponse;
    public byte CommandSlot { get; set; }
    public byte ActionType { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(CommandSlot);
        writer.Write(ActionType);
        writer.Write(new byte[BodySize - 2]);
    }
}
