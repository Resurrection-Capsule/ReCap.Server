using System.IO;
using System.Numerics;
using System.Text;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

// ServerEvent (0x9B) — server→client FX/UI event. Client-verified contract (Ghidra
// 2026-06-05): handler ClientNet::OnGmsServerEvent @0x0053ec80, 26-field reflection
// (sequence mode, 0xFF sentinel), registrar AssetData::ServerEvent @0x00f60ea0.
// Field 6 = FNV(name + ".ServerEventDef"); unresolved asset = silent skip.
// Dispatch is MUTUALLY EXCLUSIVE on field 15: clientEventID != 0 → UI-event dispatcher
// (@0x004e4c90), else → FX path (@0x0050a970). Two packets needed for FX + UI together.
// FX recipes: at-position = {6, 10}; attached = {6, 7} (+1 slot & 4 forceAttach for
// tracked slots 1-16); stop = {7, 1, 2} (+3 hardStop). Fields 17-25 = loot descriptor.
// C++ ref ServerEvent.cpp:69 has the same shape but was debug-disabled, never validated.
public class ServerEventPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ServerEvent;

    public uint SimpleSwarmEffectId { get; set; }
    public byte ObjectFxIndex { get; set; }
    public bool Remove { get; set; }
    public bool HardStop { get; set; }
    public bool ForceAttach { get; set; }
    public bool Critical { get; set; }
    public uint ServerEventDef { get; set; }
    public uint ObjectId { get; set; }
    public uint SecondaryObjectId { get; set; }
    public uint AttackerId { get; set; }
    public Vector3? Position { get; set; }
    public Vector3? Facing { get; set; }
    public Quaternion? Orientation { get; set; }
    public Vector3? TargetPoint { get; set; }
    public int TextValue { get; set; }
    public uint ClientEventId { get; set; }
    public byte ClientIgnoreFlags { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        var reflector = new ReflectionSerializer(writer, 26);

        reflector.Begin();
        if (SimpleSwarmEffectId != 0) reflector.Write(0, () => writer.Write(SimpleSwarmEffectId));
        if (ObjectFxIndex != 0) reflector.Write(1, () => writer.Write(ObjectFxIndex));
        if (Remove) reflector.Write(2, () => writer.Write(Remove));
        if (HardStop) reflector.Write(3, () => writer.Write(HardStop));
        if (ForceAttach) reflector.Write(4, () => writer.Write(ForceAttach));
        if (Critical) reflector.Write(5, () => writer.Write(Critical));
        if (ServerEventDef != 0) reflector.Write(6, () => writer.Write(ServerEventDef));
        if (ObjectId != 0) reflector.Write(7, () => writer.Write(ObjectId));
        if (SecondaryObjectId != 0) reflector.Write(8, () => writer.Write(SecondaryObjectId));
        if (AttackerId != 0) reflector.Write(9, () => writer.Write(AttackerId));
        if (Position is Vector3 position) reflector.Write(10, () => writer.Write(position));
        if (Facing is Vector3 facing) reflector.Write(11, () => writer.Write(facing));
        if (Orientation is Quaternion orientation) reflector.Write(12, () => writer.Write(orientation));
        if (TargetPoint is Vector3 targetPoint) reflector.Write(13, () => writer.Write(targetPoint));
        if (TextValue != 0) reflector.Write(14, () => writer.Write(TextValue));
        if (ClientEventId != 0) reflector.Write(15, () => writer.Write(ClientEventId));
        if (ClientIgnoreFlags != 0) reflector.Write(16, () => writer.Write(ClientIgnoreFlags));
        reflector.End();
    }
}
