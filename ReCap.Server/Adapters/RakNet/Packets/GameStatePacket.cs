using System.Text;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

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
