using System.IO;
using System.Text;

using ReCap.Server.Domain.Gameplay.Objects;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class LocomotionDataUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.LocomotionDataUpdate;
    public uint ObjectId { get; set; }
    public LocomotionData Locomotion { get; set; }

    public void ReadFrom(Stream stream)
    {
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(ObjectId);
        Locomotion?.WriteTo(stream);
    }
}
