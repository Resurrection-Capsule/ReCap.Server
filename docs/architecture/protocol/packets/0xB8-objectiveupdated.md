# 0xB8 — ObjectiveUpdated

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 24 B | [09 Dungeon](../../flow/phases/09-dungeon.md), [10 Gameloop](../../flow/phases/10-gameloop.md) | ❓ |

Delta update for a single objective. Sent during `OnPlayerStart` (per-objective init notification) and from `Instance::Update` (periodic ticker, e.g. `FinishLevelQuickly` time).

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `id` | u32 | **BE** | Objective hash. |
| `0x04` | `mId` | u8 | — | `client->GetId()` — the player it applies to. |
| `0x05` | `medal` | u8 | — | `ObjectiveMedal` enum (Bronze=1/Silver=2/Gold=3/Unknown=4 per `Types.h:154-158`). |
| `0x06` | `voiceover` | u32 | **BE** | FNV hash of voiceover name (e.g. `vo_ship_obelisk_accessed`). |
| `0x0A` | `showNotification` | bool (u8) | — | C++ hardcodes `false`. |
| `0x0B` | `value` | u32 | **BE** | Current progress value. |
| `0x0F` | `unknown1` | u32 | **BE** | Hardcoded `2` in C++. |
| `0x13` | `unknown2` | u32 | **BE** | Hardcoded `3` in C++. |

Total: 1 byte opcode + 23 byte body = **24 bytes**.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:2214-2231`:

```cpp
void Server::SendObjectiveUpdate(const ClientPtr& client, uint8_t id, uint32_t voiceover) {
    BitStream outStream(8);
    outStream.Write(PacketID::ObjectiveUpdated);

    const auto& objectives = mGame.GetObjectives();
    const auto& objective = objectives[id];

    Write<uint32_t>(outStream, objective.id);
    Write<uint8_t>(outStream, client->GetId());
    Write<uint8_t>(outStream, static_cast<uint8_t>(objective.medal));
    Write<uint32_t>(outStream, voiceover);
    Write<bool>(outStream, false); // show notification
    Write<uint32_t>(outStream, objective.value);
    Write<uint32_t>(outStream, 2); // unknown
    Write<uint32_t>(outStream, 3); // unknown

    Send(outStream, client);
}
```

Called from `Instance::Update` (`Instance.cpp:467-478`) every 50 ms for the `FinishLevelQuickly` objective to broadcast elapsed seconds.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/ObjectiveUpdatedPacket.cs` — file exists. Audit byte-for-byte vs the C++ writer; confirm field order matches `id / mId / medal / voiceover / showNotification / value / unknown1 / unknown2`.

C# `Game.OnPlayerStart` and `Game.Update` do **not** call this. Phase 09 audit item — missing.

---

## Open audit items

1. **Wire it from C# `Game.Update`** for periodic progression — at minimum the `FinishLevelQuickly` ticker.
2. **`unknown1=2` and `unknown2=3`** — what do these gate? Sometimes hardcoded values turn out to be flag bitmaps. Capture would clarify.
3. **`voiceover` parameter.** C++ takes it as an argument (not a property of the objective). C# needs the same — pass per-call, don't store on `ObjectiveData`.
4. **`ObjectiveMedal` enum values.** Verify C# uses the same byte values as C++ (`Bronze=1, Silver=2, Gold=3, Unknown=4`) or whatever the canonical layout is in `Types.h:154-158`.

---

## Related

- [Phase 09 Dungeon](../../flow/phases/09-dungeon.md) — init updates
- [Phase 10 Gameloop](../../flow/phases/10-gameloop.md) — periodic ticker
- [0xB7 ObjectivesInitForLevel](0xB7-objectivesinitforlevel.md) — full list send
- [0xB9 ObjectivesComplete](0xB9-objectivescomplete.md) — terminal summary
