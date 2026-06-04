using System.Text;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

// RakNet GameState game-mode field (client stores at simulator+0x3B420, default 0xFFFFFFFF).
// Mirrors C++ Blaze::GameType (Types.h:158-166); distinct from the Blaze matchmaking
// GameType enum (Managed/Matched/Solo). C++ Server.cpp:1369 sends Chain for the campaign;
// 0 is not a valid member (enum starts at Tutorial=1) and misdirects the client UI state
// machine (matchmaking/map-room path → cMapRoomUI null-movie crash @0x551f47).
public enum LabsGameType : uint
{
    Tutorial = 1,
    Chain = 2,
    Arena = 3,
    KillRace = 4,
    Juggernaut = 5,
    Quickplay = 6,
    DirectEntry = 7
}

public class GameStatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.GameState;

    public ulong GameTime { get; set; }
    public ulong TimeElapsed { get; set; }
    public GameState State { get; set; }
    public uint GameType { get; set; }

    private static byte WireState(GameState state) => state switch
    {
        GameState.Spaceship    => 0x02,
        GameState.ChainVoting  => 0x0B,
        GameState.PreDungeon   => 0x05,
        GameState.Dungeon      => 0x06,
        GameState.ChainCashOut => 0x0C,
        _                      => 0x02
    };

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(GameTime);
        writer.Write(TimeElapsed);
        writer.Write(WireState(State));
        writer.Write(GameType);
        writer.Write(1u);
    }
}
