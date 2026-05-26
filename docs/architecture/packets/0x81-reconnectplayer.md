# 0x81 — ReconnectPlayer

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 5 B | [11 ChainCashOut](../phases/11-chaincashout.md), [13 Disconnect](../phases/13-disconnect.md) | ❌ C# missing |

Tells the client to transition into a different `GameState` and reconnect/re-bind whatever the new state needs. Used in C++ only by `Instance::BeamOut` (`Instance.cpp:870`) to push the client from `Dungeon` → `ChainVoting` so the cashout UI can engage.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `newState` | u32 | **BE** | Target `GameState` value (see [STATE_MACHINE.md](../STATE_MACHINE.md)). |

Total: 1 byte opcode + 4 byte body = **5 bytes**.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1268-1277`:

```cpp
void Server::SendReconnectPlayer(const ClientPtr& client, GameState gameState) {
    if (client->SetGameState(gameState)) {                       // validates via IsValidStateChange
        BitStream outStream(8);
        outStream.Write(PacketID::ReconnectPlayer);
        Write<uint32_t>(outStream, static_cast<uint32_t>(gameState));   // BE wrapper
        Send(outStream, client);
    }
}
```

**Refuses to send** if `SetGameState` rejects the transition (e.g. invalid `from → to` per `Client.cpp:12-49`). Silent skip — no log on rejection from this path (`SetGameState` itself logs).

---

## C# packet class

**Not implemented.** `Adapters/RakNet/Packets/PacketActivator.cs:23-24` is an empty `case PacketType.ReconnectPlayer: break;` stub. No `ReconnectPlayerPacket.cs` file exists.

Sketch for [ROADMAP M4](../ROADMAP.md) implementation:

```csharp
public class ReconnectPlayerPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ReconnectPlayer;
    public uint NewState { get; set; }

    public void ReadFrom(Stream stream) { }
    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.WriteBE(NewState);
    }
}
```

Caller (in `Game.cs` once `BeamOut` is implemented):

```csharp
sender.SendPacket(new ReconnectPlayerPacket { NewState = (uint)GameState.ChainVoting });
```

---

## Open audit items

1. **Implement the packet class.** Trivial — 5-byte body.
2. **State validation.** C# has no `IsValidStateChange` equivalent. Decide: validate inside the packet writer? In `Game.SetState(...)`? See [STATE_MACHINE.md](../STATE_MACHINE.md) open items.
3. **Audit the call sites.** C++ uses this only from `BeamOut`. Phase 13 lists it as a candidate for graceful "back to Spaceship" flows. Confirm whether any other server-driven transition needs it.

---

## Related

- [Phase 11 ChainCashOut](../phases/11-chaincashout.md) — `BeamOut` path
- [Phase 13 Disconnect](../phases/13-disconnect.md) — potential future use
- [STATE_MACHINE.md](../STATE_MACHINE.md) — legal transitions
