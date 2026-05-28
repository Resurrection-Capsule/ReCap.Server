# 0xB0 — GamePrepareForStart

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 17 B | [08 PreDungeon](../../flow/phases/08-predungeon.md) | ✅ |

Sent once to each client after `SetSquad` resolves the chosen squad (triggered by `ChainPlayerMsgs` with `byteCount=6`). Tells the client which level and marker-set to load and signals that all players are ready to begin. The C++ writer gates on `SetGameState(PreDungeon)` — it will silently bail if the state transition is rejected.

`playerBitmask` is always `1` in single-player; C++ comment suggests it may be a per-player bit-field ("0bABCD | 1 bit per player?") for co-op.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `level` | u32 | **BE** | `chainData.GetLevel()` — level file hash/ID. |
| `0x04` | `markerSet` | u32 | **BE** | `chainData.GetMarkerSet()` — marker-set file hash/ID. |
| `0x08` | `playerBitmask` | u32 | **BE** | Hardcoded `1`. C++: "before this updates it's set to 8… 0bABCD, 1 bit per player?" |
| `0x0C` | `levelIndex` | u32 | **BE** | `chainData.GetLevelIndex()`. Must satisfy `0 ≤ index ≤ 72`. |

Total: 1 byte opcode + 4 + 4 + 4 + 4 = **17 bytes**.

> C++ comment: `// Packet size: 0x10` = 16 decimal (body only) — consistent.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1996-2069`:

```cpp
void Server::SendGamePrepareForStart(const ClientPtr& client) {
    if (!client->SetGameState(GameState::PreDungeon)) {
        return;
    }

    const auto& chainData = mGame.GetChainData();

    // Packet size: 0x10
    BitStream outStream(8);
    outStream.Write(PacketID::GamePrepareForStart);

    bool start = true;
    if (start) {
        Write<uint32_t>(outStream, chainData.GetLevel());       // level file
        Write<uint32_t>(outStream, chainData.GetMarkerSet());   // markerset file
        Write<uint32_t>(outStream, 1);                          // player readiness bitmask
        Write<uint32_t>(outStream, chainData.GetLevelIndex());  // level index (0-72)
    } else {
        Write<uint32_t>(outStream, 0);
        Write<uint32_t>(outStream, 0);
        Write<uint32_t>(outStream, 0);
        Write<uint32_t>(outStream, chainData.GetLevelIndex());
    }

    Send(outStream, client);
}
```

Called from `Server.cpp:1232` inside `PrepareGameStart`, which runs on `ChainPlayerMsgs(byteCount=6)`. `start` is unconditionally `true` — the `else` branch is dead code in current server.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/GamePrepareForStartPacket.cs:1-47`:

```csharp
public uint LevelHash { get; set; }
public uint MarkerSetHash { get; set; }
public uint PlayerBitmask { get; set; }
public uint LevelIndex { get; set; }

public void WriteTo(Stream stream)
{
    using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
    writer.WriteBE(LevelHash);
    writer.WriteBE(MarkerSetHash);
    writer.WriteBE(PlayerBitmask);
    writer.WriteBE(LevelIndex);
}
```

Constructed and sent from `Game.HandleChainPlayerMsgs` (`Domain/Gameplay/Game.cs:313`):

```csharp
var prepareStart = new GamePrepareForStartPacket(Chain.Level, Chain.MarkerSet, 1, Chain.LevelIndex);
```

All four fields BE. Matches C++ `Write<uint32_t>` wrapper exactly. `ReadFrom` is implemented but unused (S→C only).

---

## Open audit items

1. **State guard absent in C#.** C++ checks `SetGameState(PreDungeon)` before writing. C# sends unconditionally. If the client is already in PreDungeon (replay scenario), C++ would drop the packet; C# would send a duplicate. Low risk for single-player but document for co-op.
2. **`playerBitmask` co-op behaviour.** For >1 player, C++ comment suggests this becomes a bit-field (`0b0011` for 2 players, etc.). Confirm via co-op capture before multiplayer milestone.
3. **`start = false` branch.** Sends three zero fields then `levelIndex`. Determine when this path would activate (possibly spectator or re-entry) — currently dead code.
4. **`LevelHash` vs `Level`.** C# names the field `LevelHash`; C++ calls it `GetLevel()`. Confirm it's a hash and not a numeric level ID.

---

## Related

- [Phase 08 PreDungeon](../../flow/phases/08-predungeon.md) — full ChainPlayerMsgs(6) → PrepareForStart flow
- [0xAC ChainPlayerMsgs](0xAC-chainplayermsgs.md) — triggers this packet
- [0xB1 GameStart](0xB1-gamestart.md) — sent when loading completes (status=8)
- [0xA1 LabsPlayerUpdate](0xA1-labsplayerupdate.md) — next 50 ms tick carries squad/character data
