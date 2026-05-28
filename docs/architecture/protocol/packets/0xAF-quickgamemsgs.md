# 0xAF — QuickGameMsgs

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 2 B | [09 Dungeon entry](../../flow/phases/09-dungeon.md) | ⚠️ |

Sent as part of the Dungeon-entry burst (after `DirectorState`, before `ObjectCreate` calls). Single bool field: C++ sends `true` (`reset = true`). The C++ comment admits uncertainty — "if true: [set state Spaceship]" — suggesting the field may trigger a client-side reset if the game mode is over. Exact client-side branch unknown; empirically the dungeon loads when this is sent with `true`.

C# currently sends `0x00` (false) instead of `0x01` (true) — diverges from C++ reference.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `reset` | bool (u8) | — | C++ writes `true` (`0x01`) via `Write<bool>`. C# writes `0x00`. |

Total: 1 byte opcode + 1 byte body = **2 bytes**.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:2322-2332`:

```cpp
void Server::SendQuickGame(const ClientPtr& client) {
    BitStream outStream(8);
    outStream.Write(PacketID::QuickGameMsgs);

    // honestly im not 100% sure yet, ignore all
    // if true: [set state Spaceship]
    bool reset = true;
    Write<bool>(outStream, reset);

    Send(outStream, client);
}
```

Called from `Server.cpp:1197` inside the `GameState::Dungeon` branch of `OnDebugPing`, after `SendDirectorState` and before `mGame.OnPlayerStart(player)`.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/QuickGameMsgsPacket.cs:1-27`:

```csharp
public void WriteTo(Stream stream)
{
    using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
    writer.Write((byte)0);
}
```

Sent from `Game.HandleDebugPing` (`Domain/Gameplay/Game.cs:283`) in the `GameState.Dungeon` case. Writes `0x00`, not `0x01`. No property exposed — the value is hardcoded.

---

## Open audit items

1. **`reset` value diverges.** C++ sends `true` (`0x01`); C# sends `false` (`0x00`). Fix: expose a `bool Reset` property defaulting to `true` and write it. Verify dungeon loads cleanly with `0x01`.
2. **"if true: set state Spaceship" semantics.** The C++ author was uncertain. Determine via Ghidra or wire capture whether `true` arms a client-side quick-exit path or is simply ignored in the dungeon context.
3. **`Write<bool>` encoding.** Confirm RakNet `Write<bool>` emits a single byte (`0x01`/`0x00`), not a bit-packed boolean, before assuming 2-byte total.

---

## Related

- [Phase 09 Dungeon entry](../../flow/phases/09-dungeon.md) — full burst sequence
- [0x8B DirectorState](0x8B-directorstate.md) — sent immediately before this
- [0xCC DebugPing](0xCC-debugping.md) — triggers the Dungeon-entry burst
