# 0x83 — Goodbye

| Direction | Size | Phase | Status |
|---|---|---|---|
| C→S | ❓ unknown | [13 Disconnect](../phases/13-disconnect.md) | ❌ |

Client-side graceful disconnect signal. Sent (presumably) before the UDP socket closes so the server can drop the session cleanly instead of waiting for `ID_DISCONNECTION_NOTIFICATION` / `ID_CONNECTION_LOST`.

**Neither C++ nor C# parses this packet.** Body layout is undocumented because no code reads it.

---

## Body layout

❓ Unknown. Capture-required.

Likely shapes (none verified):

- 0 bytes (pure signal)
- 1 byte reason code
- 4 bytes status

Without a wire capture from the official client tearing down, any guess is speculation.

---

## C++ reader

**Absent.** `Server::ParseSporeNetPackets` (`Server.cpp:458+`) has no `case PacketID::Goodbye`. The packet ID is declared in `Server.h:40` (`constexpr MessageID Goodbye = 0x83;`) and `Types.h:29` but never handled.

The client's `Goodbye` packet is silently consumed by the unhandled-default branch.

---

## C# packet class

**Absent.** `Adapters/RakNet/PacketType.cs:9` declares `Goodbye = 0x83`. `Adapters/RakNet/Packets/PacketActivator.cs:30-31` is an empty `case PacketType.Goodbye: break;` stub. No `GoodbyePacket.cs` file.

Result: `PacketActivator.CreateInstance` returns `null` for any incoming Goodbye, which trips the logger:

```
RakNet: Peer 0x... has sent an unhandled packet (Goodbye)! Skipping...
```

Then `Game.HandlePacket` is never invoked, the session continues until RakNet detects the closed socket and fires `ID_DISCONNECTION_NOTIFICATION`. Functional, just not graceful.

---

## Open audit items

1. **Capture the body.** Force a clean exit from the official client and hex-dump the last packet sent to port 42000. Settle the layout.
2. **Implement the parser** (`GoodbyePacket.cs`) once the body is known. Trigger `Game.DetachPlayer(client)` + `Clients.Remove(...)` early instead of waiting for the RakNet timeout.
3. **Reason code distinction.** If the body carries a code, surface it as `session.Disconnected += reason => …` (currently discarded — see [Phase 13](../phases/13-disconnect.md)).
4. **Compare to retail-server behaviour.** The reference dev never wired this — was the retail Maxis server ignoring it too, or did they have a separate handler the source dump doesn't show?

---

## Related

- [Phase 13 Disconnect](../phases/13-disconnect.md) — overall disconnect flow + leak shape
- [0x86 PlayerDeparted](0x86-playerdeparted.md) — server-side broadcast that should fire after Goodbye
