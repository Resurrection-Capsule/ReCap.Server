using System.IO;
using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// ActionCommandResponse (0xA8), raw LE 56B body — client handler @0x0053cb10 reads exactly
// 0x38 bytes, routes on byte +1 (FUN_004d9ba0): 1=ability ack, 2=complete (clears pending
// command lock @block+4 set by FUN_004d9150 with a 3000ms deadline, resets hero anim),
// 4=stop ack, 8=movement GO, 0x10=clear. Case-2 gate: byte +0 must echo the rolling command
// stamp the client sent in ActionCommandMsgs byte +0x01 (counter DAT_011205e0, stored at
// block+8). UserData (+0x34) >= 0 writes client DAT_0143fe3c (FUN_004e2030); 0xFFFFFFFF skips.
// C++ ref: Server.cpp:1659 (type 1 only, same 56B shape).
public class ActionCommandResponsePacket : IRakNetPacket
{
    public PacketType Type => PacketType.ActionCommandResponse;

    public byte SyncStamp { get; set; }
    public byte ResponseType { get; set; }
    public uint ObjectId { get; set; }
    public uint Flags08 { get; set; }
    public ulong Time10 { get; set; }
    public ulong TimeImmobilizedUntil { get; set; }
    public ulong TimeCooldownUntil { get; set; }
    public ulong Time28 { get; set; }
    public uint UserData { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(SyncStamp);
        writer.Write(ResponseType);
        writer.Write((ushort)0);
        writer.Write(ObjectId);
        writer.Write(Flags08);
        writer.Write((uint)0);
        writer.Write(Time10);
        writer.Write(TimeImmobilizedUntil);
        writer.Write(TimeCooldownUntil);
        writer.Write(Time28);
        writer.Write((uint)0);
        writer.Write(UserData);
    }
}
