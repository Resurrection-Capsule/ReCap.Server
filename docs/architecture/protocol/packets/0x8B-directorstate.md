# 0x8B — DirectorState

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 0x4D0 B (fixed raw blob) | [09 Dungeon Entry](../../flow/phases/09-dungeon.md) | ⚠️ |

Broadcasts the AI director's current state to the client. Sent once on dungeon entry (DebugPing Dungeon case) with an empty/default director (mBossId=0, mbBossSpawned=false). The C++ uses `cAIDirector::WriteTo` (raw fixed-size blob), not `WriteReflection`.

The blob is 0x4D0 bytes. Most bytes are zero-initialized; only a handful of offsets carry live data. The C# implementation writes only 16 bytes — a significant divergence from the C++ blob size.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00D` | `mbBossSpawned` | bool (1 B) | — | `Write<bool>` wrapper → BE |
| `0x00E` | `mbBossHorde` | bool (1 B) | — | BE |
| `0x00F` | `mbCaptainSpawned` | bool (1 B) | — | BE |
| `0x010` | `mbBossComplete` | bool (1 B) | — | BE |
| `0x014` | `mBossId` | u32 | **BE** | `Write<tObjID>` wrapper |
| `0x47C` | `mActiveHordeWaves` | i32 | **BE** | `Write<int32_t>` wrapper |
| `0x48C` | `mbHordeSpawned` | bool (1 B) | — | BE |
| _rest_ | _(zero padding)_ | — | — | Total blob = 0x4D0 bytes |

The `ReallocateStream` call reserves the full 0x4D0-byte region. `SetWriteOffset` jumps to each field; all other bytes remain zero.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1411-1419`:

```cpp
void Server::SendDirectorState(const ClientPtr& client, const cAIDirector& director) {
    BitStream outStream(8);
    outStream.Write(PacketID::DirectorState);

    // director.WriteReflection(outStream);
    director.WriteTo(outStream);

    Send(outStream, client);
}
```

`recap_server_develop/darkspore_server/source/RakNet/Types.cpp:147-168`:

```cpp
void cAIDirector::WriteTo(BitStream& stream) const {
    constexpr auto size = bytes_to_bits(0x4D0);
    auto writeOffset = ReallocateStream(stream, size);

    stream.SetWriteOffset(writeOffset + bytes_to_bits(0x00D));
    Write<bool>(stream, mbBossSpawned);
    Write<bool>(stream, mbBossHorde);
    Write<bool>(stream, mbCaptainSpawned);
    Write<bool>(stream, mbBossComplete);

    stream.SetWriteOffset(writeOffset + bytes_to_bits(0x014));
    Write<tObjID>(stream, mBossId);

    stream.SetWriteOffset(writeOffset + bytes_to_bits(0x47C));
    Write<int32_t>(stream, mActiveHordeWaves);

    stream.SetWriteOffset(writeOffset + bytes_to_bits(0x48C));
    Write<bool>(stream, mbHordeSpawned);

    stream.SetWriteOffset(writeOffset + size);
}
```

Call site on dungeon entry — `Server.cpp:1191-1195`:

```cpp
cAIDirector director;
director.mBossId = 0;
director.mbBossSpawned = false;
SendDirectorState(client, director);
```

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/DirectorStatePacket.cs`:

```csharp
public void WriteTo(Stream stream)
{
    using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
    writer.WriteBE(1u); // Enabled
    writer.WriteBE(0u); // State
    writer.WriteBE(0u); // IntensityState
    writer.WriteBE(0f); // Intensity
}
```

The C# writes 16 bytes total (4 × 4B). The C++ writes a 0x4D0-byte fixed blob with fields at specific offsets. The four fields the C# writes (`mEnabled`, `mState`, `mIntensityState`, `mIntensity`) do not correspond to any `cAIDirector` members — they appear to be remnants of a stale or incorrect understanding of the struct. `cAIDirector` in C++ has: `mBossId`, `mActiveHordeWaves`, `mbBossSpawned`, `mbBossHorde`, `mbCaptainSpawned`, `mbBossComplete`, `mbHordeSpawned`.

---

## Open audit items

1. **Blob size mismatch (critical).** C# emits 16 bytes; C++ emits 0x4D0 bytes. Client likely reads the full blob and will fault or desync if it receives only 16 bytes.
2. **Wrong fields.** C# writes `mEnabled/mState/mIntensityState/mIntensity` — none of these exist in `cAIDirector`. Implement `WriteTo` to zero-fill 0x4D0 bytes and poke the correct field offsets.
3. **`WriteReflection` path exists.** C++ comments out `WriteReflection` in favor of `WriteTo`. If a future version switches to reflection encoding, note it uses `reflection_serializer<7>` (1-byte bitmap) with 7 fields (`Types.cpp:170-181`).
4. **Periodic director broadcast.** `Server.cpp:372-376` shows a second call site where `director.mBossId = player->GetCharacterObject(0)->GetId()`. This populates a live boss ID. The C# path never sets this.

---

## Related

- [Phase 09 Dungeon Entry](../../flow/phases/09-dungeon.md) — call sequence
- `recap_server_develop/darkspore_server/source/RakNet/Types.h:427-441` — `cAIDirector` struct definition
- `recap_server_develop/darkspore_server/source/RakNet/Types.cpp:147-181` — `WriteTo` + `WriteReflection` bodies
