using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Gameplay;

// Locks in the single-threaded-simulation invariant introduced to break the RakNexus/_luaGate
// lock-ordering deadlock: inbound gameplay packets are QUEUED (never handled on the receive thread)
// and drained by Game.Update on the game-loop thread.
public class InboundQueueTests
{
    [Fact]
    public void EnqueueInbound_Defers_UntilUpdateDrains()
    {
        var game = new Game(1, GameType.Matched);

        // A packet type not in HandlePacket's switch → drained as a harmless no-op (no session needed).
        game.EnqueueInbound(null!, new GameStatePacket());
        game.EnqueueInbound(null!, new GameStatePacket());

        Assert.Equal(2, game.PendingInboundCount); // still queued: enqueue must not handle inline

        game.Update();

        Assert.Equal(0, game.PendingInboundCount); // Update drained the queue on the game-loop thread
    }
}
