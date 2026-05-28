using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

/// <summary>
/// ObjectiveUpdated (0xB8) â€” updates a single objective's progress.
/// C++: SendObjectiveUpdate
///
/// Format:
///   [u32 objectiveId]
///   [u8  clientId]       // player/client id
///   [u8  medal]          // ObjectiveMedal enum: 0=InProgress,1=Failed,2=Bronze,3=Silver,4=Gold
///   [u32 voiceover]      // hash_id of VO clip (0 = none)
///   [bool showNotif]     // whether to show notification
///   [u32 value]          // current progress value
///   [u32 unk1]           // always 2
///   [u32 unk2]           // always 3
/// </summary>
public class ObjectiveUpdatedPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectiveUpdated;

    public uint ObjectiveId    { get; set; }
    public byte ClientId       { get; set; }
    public byte Medal          { get; set; } // 0 = InProgress
    public uint Voiceover      { get; set; } = 0;
    public bool ShowNotif      { get; set; } = false;
    public uint Value          { get; set; } = 0;

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(ObjectiveId);
        stream.WriteByte(ClientId);
        stream.WriteByte(Medal);
        writer.Write(Voiceover);
        stream.WriteByte(ShowNotif ? (byte)1 : (byte)0);
        writer.Write(Value);
        writer.Write((uint)2); // unk1
        writer.Write((uint)3); // unk2
    }
}
