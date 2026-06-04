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

        // C++ Server::SendQuickGame (Server.cpp:2346) writes `Write<bool>(reset=true)` → 0x01,
        // on the Dungeon path (Server.cpp:1199, right after DirectorState, before OnPlayerStart).
        // The C++ source comment "if true: set state Spaceship" is the author's own unsure guess
        // ("not 100% sure, ignore all") — empirically C++ sends 0x01 here and the client plays the
        // dungeon, so 0x01 is the correct dungeon-entry value. C# was sending 0x00.
        writer.Write((byte)1);
    }
}
