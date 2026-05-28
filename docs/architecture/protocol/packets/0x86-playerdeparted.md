# 0x86 — PlayerDeparted

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 2 B | [13 Disconnect](../../flow/phases/13-disconnect.md) | ❌ |

Server broadcasts that a player slot just emptied. Mirror of [0x84 PlayerJoined](0x84-playerjoined.md).

**Dead code in both implementations** — defined / declared, never called.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `mId` | u8 | — | Departing player's session index. |

Total: 1 byte opcode + 1 byte body = **2 bytes**.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1297-1304`:

```cpp
void Server::SendPlayerDeparted(const ClientPtr& client) {
    // Packet size: 0x01
    BitStream outStream(8);
    outStream.Write(PacketID::PlayerDeparted);
    outStream.Write<uint8_t>(client->mId);

    Send(outStream, client);
}
```

**Zero call sites in the C++ source.** Defined but never invoked. `Server::RemoveClient` (`Server.cpp:516-520`) — the natural caller — only erases the session map, it doesn't broadcast.

---

## C# packet class

**Not implemented.** `Adapters/RakNet/PacketType.cs:12` declares `PlayerDeparted = 0x86`. `Adapters/RakNet/Packets/PacketActivator.cs:41-42` is an empty `case PacketType.PlayerDeparted: break;` stub. No `PlayerDepartedPacket.cs` file.

Sketch for implementation (mirroring `PlayerJoinedPacket`):

```csharp
public class PlayerDepartedPacket : IRakNetPacket
{
    public PacketType Type => PacketType.PlayerDeparted;
    public byte PlayerId { get; set; }

    public void ReadFrom(Stream stream) { }
    public void WriteTo(Stream stream) => stream.WriteByte(PlayerId);
}
```

Caller (in `RakNetServer.OnSessionDisconnected` or `Game.DetachPlayer`):

```csharp
foreach (var other in game.Players.Values.Where(p => p != departingPlayer))
{
    other.Client?.SendPacket(new PlayerDepartedPacket { PlayerId = departingPlayer.Slot });
}
```

---

## Open audit items

1. **Wire it up on the C# side.** Phase 13 audit item #2 — multi-client lobby UIs need this to update the roster.
2. **Symmetric to `PlayerJoined`.** Implement both at once so the protocol stays internally consistent.
3. **Broadcast scope.** Only to *other* clients in the same `Game`. Never to the leaving client (their session is already torn down).
4. **C++ retail behaviour.** Was the published reference's dead-code state intentional (Darkspore being mostly-singleplayer for these flows) or just unfinished?

---

## Related

- [Phase 13 Disconnect](../../flow/phases/13-disconnect.md) — cleanup leak that this packet would close
- [0x84 PlayerJoined](0x84-playerjoined.md) — symmetric join
