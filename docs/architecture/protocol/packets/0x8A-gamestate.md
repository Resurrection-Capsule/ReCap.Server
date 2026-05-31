# 0x8A — GameState

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 26 B | [10 Game Loop](../../flow/phases/10-gameloop.md) | ⚠️ |

Broadcast every 50 ms to every connected client. Carries the server's monotonic clock, objective-timer value, wire-state byte, game-type, and a fixed `1` sentinel. `GameType` is frozen at `0` in the update loop — the C++ `data.type` field defaults to zero in `mStateData` and is never modified outside of non-Tier-1 states.

The C# writer passes both `GameTime` and `TimeElapsed` as the same `TotalMilliseconds` since session start, whereas C++ populates them from two distinct `mGame` accessors (`GetTime()` / `GetTimeElapsed()`). Semantics unknown — marked ⚠️.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `gameTime` | u64 | **LE** | `mGame.GetTime()` — milliseconds since instance start. |
| `0x08` | `timeElapsed` | u64 | **LE** | `mGame.GetTimeElapsed()` — objective completion timer. May differ from `gameTime`. |
| `0x10` | `state` | u8 | — | Wire state byte. Spaceship=`0x02`, ChainVoting=`0x0B`, PreDungeon=`0x05`, Dungeon=`0x06`, ChainCashOut=`0x0C`. |
| `0x11` | `type` | u32 | **LE** | `GameType`. Hardcoded `0` in loop (C++ `mStateData` zero-initialized). |
| `0x15` | `fixed1` | u32 | **LE** | Always `1`. C++ comment: `mov [simulator+3B41Ch], value (default: 0)`. |

> ⚠️ SUPERSEDED 2026-05-31 — see VERIFIED_FACTS.md (fields corrected to LE; Write&lt;T&gt; is LE on wire)

Total: 1 byte opcode + 8 + 8 + 1 + 4 + 4 = **26 bytes**.

> Comment in C++: `// Packet size: 0x19` = 25 decimal, which is the body alone (25 B) — consistent.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1314-1337`:

```cpp
void Server::SendGameState(const ClientPtr& client, const GameStateData& data) {
    BitStream outStream(8);
    outStream.Write(PacketID::GameState);

    // Game time
    Write<uint64_t>(outStream, mGame.GetTime());

    // Game objective completion time
    Write<uint64_t>(outStream, mGame.GetTimeElapsed());
    Write<uint8_t>(outStream, static_cast<uint8_t>(data.state));

    // Game type
    Write<uint32_t>(outStream, static_cast<uint32_t>(data.type));

    // sub_9C16D0 - unused?
    Write<uint32_t>(outStream, 1);  // mov [simulator+3B41Ch], value (default: 0)

    Send(outStream, client);
}
```

Broadcast loop at `Server.cpp:185-208`: called once per client per tick when `mGame.Update()` returns true. Each client gets its own `clientGameStateData` (pulled from `client->GetGameStateData()`), so `data.state` can theoretically differ per-client.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/GameStatePacket.cs:1-37`:

```csharp
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

public void WriteTo(Stream stream)
{
    using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
    writer.WriteBE(GameTime);
    writer.WriteBE(TimeElapsed);
    writer.Write(WireState(State));
    writer.WriteBE(GameType);
    writer.WriteBE(1u);
}
```

Called from `Game.Update()` (`Domain/Gameplay/Game.cs:43-50`). Both `GameTime` and `TimeElapsed` are set to `(DateTime.UtcNow - StartTime).TotalMilliseconds` — single source, no separate objective timer.

`GameType = 0` is hardcoded at the call site. C++ `mStateData` is zero-initialized; do not change.

The `WireState` map at lines 16–24 covers all Tier-1 states. See [STATE_MACHINE.md](../STATE_MACHINE.md).

---

## Open audit items

1. **`GetTime()` vs `GetTimeElapsed()` semantics.** C# uses the same value for both. C++ has two distinct accessors. Determine if `timeElapsed` is the objective countdown timer (stops when objective completes?) or something else; align once dungeon gameplay is active.
2. **Per-client `data.state` divergence.** C++ copies `gameStateData.var` and `gameStateData.type` per client but reads `state` from `client->GetGameStateData()` directly. C# broadcasts a single `State` field for all players. Revisit for multiplayer.
3. **`fixed1` semantics.** C++ comment says unused (`sub_9C16D0 - unused?`). Confirm client parses it or just reads past it.
4. **Wire capture.** Verify 26-byte total against a real packet dump.

---

## Related

- [Phase 10 Game Loop](../../flow/phases/10-gameloop.md) — broadcast context
- [STATE_MACHINE.md](../STATE_MACHINE.md) — wire state enum
- [0xCC DebugPing](0xCC-debugping.md) — also periodic; triggers state transitions
