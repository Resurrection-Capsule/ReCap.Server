using System.IO;
using System.Text;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class DirectorStatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.DirectorState;

    public void ReadFrom(Stream stream) { }

    // C++ cAIDirector::WriteTo (Types.cpp:147) emits a fixed 0x4D0-byte (1232) blob.
    // At Dungeon entry every field is zero (no boss/horde), so a zeroed blob is exact.
    // Layout for reference (Write<T> wrapper, byte-swapped):
    //   0x00D bool mbBossSpawned, 0x00E mbBossHorde, 0x00F mbCaptainSpawned,
    //   0x010 bool mbBossComplete, 0x014 u32 mBossId,
    //   0x47C i32 mActiveHordeWaves, 0x48C bool mbHordeSpawned. Remainder zero.
    private const int BlobSize = 0x4D0;

    public void WriteTo(Stream stream)
    {
        stream.Write(new byte[BlobSize], 0, BlobSize);
    }
}
