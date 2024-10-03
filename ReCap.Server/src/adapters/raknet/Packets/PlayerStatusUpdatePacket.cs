using System.Text;

namespace ReCap.RakNet.Packets;

public class PlayerStatusUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.PlayerStatusUpdate;

    public uint Status { get; set; }
    public float Progress { get; set; }

    public PlayerStatusUpdatePacket()
    {
    }

    public PlayerStatusUpdatePacket(uint status, float progress)
    {
        Status = status;
        Progress = progress;
    }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        Status = reader.ReadUInt32();
        Progress = reader.ReadSingle();
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(Status);
        writer.Write(Progress);
    }
}
