# 0x88 — PlayerStatusUpdate

| Direction | Size | Phase | Status |
|---|---|---|---|
| C→S | 9 B (1 opcode + 4 status + 4 progress) | [08 PreDungeon](../../flow/phases/08-predungeon.md), [11 ChainCashOut](../../flow/phases/11-chaincashout.md) | ⚠️ C# reads LE, C++ reads BE |
| S→C | 2 B (1 opcode + 1 playerState) | [08 PreDungeon](../../flow/phases/08-predungeon.md) | ❓ C# WriteTo emits 8 bytes wrong shape |

Bidirectional status sync packet. **The two directions have completely different shapes** — C→S carries a u32 status code and f32 progress; S→C carries a single u8 `playerState` byte. The server reacts to incoming status to drive the phase state machine (Dungeon transition on `status=0x08`, BeamOut on `status=0x20`), then always fires `SendLabsPlayerUpdate` immediately after.

The S→C form is only called from `SendPlayerStatusUpdate` in the C++ tree but has **no observed call site in the C# server** — it is declared but never sent.

---

## Body layout — C→S (client sends to server)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `Status` | u32 | **BE** | C++ `Read<uint32_t>` wrapper → BE read. See caveat below. |
| `0x04` | `Progress` | f32 | **BE** | C++ `Read<float>` wrapper → BE read. |

Total: 1 opcode + 8 body = **9 bytes**.

Known `Status` values:

| Value | Meaning | Server action |
|---|---|---|
| `0x02` | Joining | No extra action |
| `0x04` | Loading | No extra action |
| `0x08` | Loaded | State → Dungeon; `SendGameStart` + `SendDebugPing` |
| `0x20` | Beam out | `mGame.BeamOut(player)` |

> ⚠️ **C# endianness divergence:** `PlayerStatusUpdatePacket.ReadFrom` uses `BinaryReader.ReadUInt32()` and `ReadSingle()` — both **LE**. The C++ reader uses `Read<uint32_t>` and `Read<float>` (the BE wrapper). If the client sends BE (which the C++ reader implies), the C# decode is wrong. Needs wire-capture verification.

---

## Body layout — S→C (server sends to client)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `PlayerState` | u8 | n/a | Raw `outStream.Write<uint8_t>` — 1 byte, no swap. Semantics unknown. |

Total: 1 opcode + 1 body = **2 bytes**.

---

## C++ reader (server receives C→S)

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:643-686`:

```cpp
void Server::OnPlayerStatusUpdate(const ClientPtr& client) {
    const auto& player = client->GetPlayer();
    if (!player) { return; }

    uint32_t status, oldStatus;
    Read<uint32_t>(mInStream, status);          // BE wrapper

    float progress, oldProgress;
    Read<float>(mInStream, progress);            // BE wrapper

    player->GetStatus(oldStatus, oldProgress);
    player->SetStatus(status, progress);

    switch (status) {
        case 0x02: { break; }
        case 0x04: { break; }
        case 0x08: {
            auto& gameStateData = client->GetGameStateData();
            gameStateData.state = static_cast<uint32_t>(GameState::Dungeon);
            gameStateData.type = Blaze::GameType::Chain;
            SendGameStart(client);
            SendDebugPing(client);
            break;
        }
        case 0x20: { mGame.BeamOut(player); break; }
    }

    SendLabsPlayerUpdate(client, player);
    player->ResetUpdateBits();
}
```

## C++ writer (server sends S→C)

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1306-1312`:

```cpp
void Server::SendPlayerStatusUpdate(const ClientPtr& client, uint8_t playerState) {
    BitStream outStream(8);
    outStream.Write(PacketID::PlayerStatusUpdate);
    outStream.Write<uint8_t>(playerState);      // raw LE (single byte, no swap needed)
    Send(outStream, client);
}
```

`SendPlayerStatusUpdate` has **no call sites** in the C++ tree beyond its declaration in `Server.h:174`. It is dead code in the current reference implementation.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/PlayerStatusUpdatePacket.cs`:

```csharp
public class PlayerStatusUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.PlayerStatusUpdate;
    public uint Status { get; set; }
    public float Progress { get; set; }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        Status = reader.ReadUInt32();           // ⚠️ LE — should be BE to match C++
        Progress = reader.ReadSingle();         // ⚠️ LE — should be BE to match C++
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(Status);                   // ⚠️ emits u32+f32 — wrong shape for S→C (should be u8 playerState)
        writer.Write(Progress);
    }
}
```

The C# class conflates both directions into one shape. `WriteTo` emits the C→S shape (8 bytes) not the S→C shape (1 byte `playerState`). Since `SendPlayerStatusUpdate` is never called in C# either, this is currently inert.

Dispatched via `Game.HandlePacket` switch → `HandlePlayerStatusUpdate` (`Game.cs:334-356`).

---

## Open audit items

1. **Settle C→S endianness.** C++ uses BE wrappers for `Status` and `Progress`; C# reads LE. Wire-capture required — compare raw bytes against Blaze session log to confirm which side is wrong.
2. **S→C is never sent.** Neither C++ nor C# ever calls `SendPlayerStatusUpdate`. Determine whether the client expects it at any point. If not, mark as reserved.
3. **`WriteTo` shape is wrong for S→C.** If the S→C form is ever needed, `WriteTo` must be rewritten to emit a single `u8 playerState`, not `u32 Status + f32 Progress`. Consider splitting into a separate outbound packet class.
4. **`status=0x20` BeamOut path.** C# `Game.HandlePlayerStatusUpdate` has no `case 0x20` — `BeamOut` is unimplemented. Verify whether the client sends 0x20 during normal ChainCashOut flow.
5. **Progress field usage.** C++ logs it but never acts on it. Confirm whether `progress` drives any UI element on the client side (loading bar?) or is purely telemetry.

---

## Related

- [Phase 08 PreDungeon](../../flow/phases/08-predungeon.md) — primary call site: status 2→4→8 sequence drives dungeon load
- [Phase 11 ChainCashOut](../../flow/phases/11-chaincashout.md) — status=0x20 BeamOut path
- [0xA1 LabsPlayerUpdate](0xA1-labsplayerupdate.md) — always sent immediately after `OnPlayerStatusUpdate`
- [0xB1 GameStart](0xB1-gamestart.md) — sent when status=0x08
- [0xCC DebugPing](0xCC-debugping.md) — sent when status=0x08
