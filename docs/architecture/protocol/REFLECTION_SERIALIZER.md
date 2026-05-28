# Reflection Serializer

Delta-encoding format used inside Darkspore's RakNet packets. A presence bitmap (or field-ID stream) prefixes the payload, then only the *present* fields are written. Driven by `mDataBits` on the sending side: bit `i` set ⇒ field `i` emitted.

This page is the **single source of truth** for the bitmap rules. Phase docs that previously inlined the rule (notably [Phase 06 Spaceship](../flow/phases/06-spaceship.md), [Phase 08 PreDungeon](../flow/phases/08-predungeon.md), [Phase 10 Gameplay loop](../flow/phases/10-gameloop.md)) should link here.

---

## The three regimes

| Field count `N` | Header | Notation |
|---|---|---|
| `N ≤ 8` | **1 byte** little-endian bitmap | `bm1` |
| `9 ≤ N ≤ 16` | **2 byte big-endian bitmap** | `bm2` |
| `N > 16` | **byte-per-field** stream, terminated by `0xFF` | `bmID` |

The header is written **first**, then the field bodies in ascending field-index order. The decision is fixed by the writer's template argument — there is no runtime fallback.

---

## C++ — `RakNet::reflection_serializer<N>`

Source: `recap_server_develop/darkspore_server/source/RakNet/Types.h:300-440`.

```cpp
template<uint8_t FieldCount>
class reflection_serializer {
    static_assert(FieldCount > 0x00 && FieldCount <= 0xFF, "Fields must be above 0");

public:
    reflection_serializer(RakNet::BitStream& stream)
        : mStream(stream),
          mStartOffset(std::numeric_limits<BitSize_t>::max()),
          mWriteBits(0) {}

    void begin() {
        if (mStartOffset != std::numeric_limits<BitSize_t>::max()) return;
        mStartOffset = mStream.GetWriteOffset();
        mWriteBits = 0;
        if constexpr (FieldCount <= 8)       mStream.SetWriteOffset(mStartOffset + bytes_to_bits(sizeof(uint8_t)));
        else if constexpr (FieldCount <= 16) mStream.SetWriteOffset(mStartOffset + bytes_to_bits(sizeof(uint16_t)));
    }

    void end() {
        if constexpr (FieldCount > 16) {
            Write<uint8_t>(mStream, 0xFF);          // terminator
        } else {
            auto offset = mStream.GetWriteOffset();
            mStream.SetWriteOffset(mStartOffset);
            if constexpr (FieldCount <= 8) Write<uint8_t>(mStream, static_cast<uint8_t>(mWriteBits));
            else                           Write<uint16_t>(mStream, mWriteBits);   // BE via Write wrapper
            mStream.SetWriteOffset(offset);
        }
        mStartOffset = max();
    }

    template<uint8_t Field, typename T>
    void write(const T& value) {
        if constexpr (FieldCount > 16) Write<uint8_t>(mStream, Field);
        else                           mWriteBits |= 1 << Field;
        /* … then write the payload via the same Write wrapper that does bswap … */
    }
};
```

Key consequences:

- `bm1`: bitmap is **raw u8** — neither bswap'd nor wrapper-written. Bit `i` = `1 << i`.
- `bm2`: bitmap is **written via `Write<uint16_t>`** which goes through `bswap` → **Big-Endian on the wire** (high byte first).
- `bmID`: each present field's index is written as a raw `u8` *before* its payload; absent fields contribute nothing; `0xFF` is the terminator.
- Field bodies use the same `Write<T>` wrapper as everything else → primitives are BE (see [ENDIANNESS.md](ENDIANNESS.md)).
- **Class-typed fields** (`std::is_class_v<T>`) call `value.WriteTo(mStream)` instead of `Write<T>`. That `WriteTo` may itself recurse into a nested reflection serializer or emit a fixed-size raw block.
- `std::array<T, S>` fields emit each element back-to-back without an outer length prefix. Array length is implicit (compile-time `S`).

### Observed `FieldCount` template instantiations

Compiled from `grep`'ing `reflection_serializer<N>` across the source tree:

| `N` | Regime | Used by |
|---|---|---|
| 2 | bm1 | `Object.cpp:121` (interactable data), `Types.cpp:385`, `Types.cpp:860` |
| 3 | bm1 | `Object.cpp:177`, `Types.cpp:884`, `Types.cpp:948` |
| 5 | bm1 | `Types.cpp:568` |
| 7 | bm1 | `Types.cpp:171` (catalyst — see below) |
| 8 | bm1 | `Types.cpp:980` |
| 9 | bm2 | `Types.cpp:605` |
| 10 | bm2 | `Types.cpp:416`, `Types.cpp:918` |
| 11 | bm2 | `Types.cpp:1027` |
| 15 | bm2 | `Types.cpp:650` |
| 18 | bmID | `Types.cpp:823` |
| 23 | bmID | `Types.cpp:517` (locomotion) |
| **24** | bmID | **`Player::WriteReflection` (`Player.cpp:480`)** |
| **124** | bmID | **`Character::WriteReflection`** |

---

## C# — `Adapters/RakNet/ReflectionSerializer.cs`

Mirrors the C++ template but parameterised at runtime. Source-of-truth lines:

```csharp
public ReflectionSerializer(BinaryWriter writer, int fieldCount) { … }

public void Begin() {
    if (_startOffset != -1) return;
    _startOffset = _writer.BaseStream.Position;
    _writeBits = 0;
    if      (_fieldCount <= 8)  _writer.Write((byte)0);      // bm1 placeholder
    else if (_fieldCount <= 16) _writer.Write((ushort)0);    // bm2 placeholder
    // bmID: nothing written up front
}

public void End() {
    if (_fieldCount > 16) {
        _writer.Write((byte)0xFF);                            // bmID terminator
    } else {
        var endOffset = _writer.BaseStream.Position;
        _writer.BaseStream.Position = _startOffset;
        if (_fieldCount <= 8) _writer.Write((byte)_writeBits);
        else                  _writer.WriteBE(_writeBits);    // bm2 BE
        _writer.BaseStream.Position = endOffset;
    }
    _startOffset = -1;
}

public void Write(byte field, Action writeAction) {
    if (_fieldCount > 16) _writer.Write(field);               // bmID: u8 field-ID
    else                  _writeBits |= (ushort)(1 << field); // bm1/bm2: set bit
    writeAction();                                            // caller emits payload
}
```

### Differences from C++

| Aspect | C++ | C# |
|---|---|---|
| Field count | compile-time template arg | runtime ctor arg |
| Payload write | `if constexpr` chain: primitives / `glm::vec*` / `class.WriteTo` | caller's `Action writeAction` lambda — fully manual |
| `std::array<T,S>` handling | built-in: writes each element | caller must loop manually |
| Field index check | `static_assert(Field < FieldCount)` | `throw ArgumentOutOfRangeException` (`field >= _fieldCount`) |
| `bm2` endianness | C++ `Write<u16>` → `bswap` → BE | C# `WriteBE(ushort)` extension → BE explicitly |

### `BinaryWriter` extension `WriteBE`

Lives in `ReCap.Server.Util`. Without it `_writer.Write((ushort)...)` would emit little-endian (BinaryWriter default), which would break the `bm2` regime against any client expecting BE.

---

## Worked example — `Player::WriteReflection` (24 fields, bmID)

Source: `Player.cpp:478-510`.

```
+----+--------------------+
| 00 | DataSetup (bool)   |   ← only if dataBit 0 set
+----+--------------------+
| 01 | CurrentDeckIndex   |   ← only if dataBit 1
+----+--------------------+
| 02 | QueuedDeckIndex    |   ← only if dataBit 2
+----+--------------------+
| 03 | mCharacterData[3]  |   ← only if dataBit 3, 3× Character::WriteTo back-to-back, 0x620 B each
+----+--------------------+
…   …
+----+--------------------+
| 0D | mCatalysts[9]      |   ← only if dataBit 13, 9× Catalyst::WriteTo, 16 B each — yes nine even though logical 8
+----+--------------------+
| 0E | mCatalystBonuses[8]|   ← only if dataBit 14, bool[8] = 8 B
+----+--------------------+
…   …
| 17 | DeckScore          |   ← only if dataBit 23
+----+--------------------+
| FF | terminator         |
+----+--------------------+
```

Wire stream skeleton for `dataBits = {0,4,5,6,7,8,12,15,16,18,21,22}` (the C# **FROZEN** initial set, see [CLAUDE.md](../../../CLAUDE.md) and [Phase 06](../flow/phases/06-spaceship.md)):

```
00 [DataSetup byte]
04 [PlayerIndex u32 BE]
05 [Team u32 BE]
06 [PlayerOnlineId u64 BE]
07 [Status u32 BE]
08 [StatusProgress f32 BE]
0C [DNA f32 BE]
0F [AvatarLevel u32 BE]
10 [AvatarXP u32 BE]
12 [LockCamera bool]
15 [LockedAbilityMin u32 BE]
16 [LockedDeckIndexMin u32 BE]
FF
```

Field IDs are byte values (`0x00`..`0x17`), so the terminator `0xFF` is unambiguous as long as the writer never registers field `0xFF`.

> ⚠️ **Field 0xFF collision.** The `static_assert` only checks `Field < FieldCount`. Future schemas with `FieldCount > 0xFE` would clash with the terminator. Don't go there.

---

## Worked example — `Character::WriteReflection` (124 fields, bmID)

Defined in `Character.cpp`. `124 > 16` ⇒ bmID. Same encoding shape as `Player`, just more potential field IDs.

`Character()` ctor (`Character.cpp:11`) sets **all 13** of the standard `mDataBits` (fields 0–12) before any setter runs, so the first emit of any newly-created `Character` carries every standard field. Field 13 onwards is attribute data (`mPartAttributes`) — driven separately by `dataBits.set(13+i)` for `i < 111`.

---

## Worked example — `Catalyst::WriteReflection` (7 fields, bm1)

`Types.cpp:171`. 7 fields ⇒ single-byte bitmap. Only ever sent **inside** a parent payload (it has no top-level dispatch).

Catalysts also expose a separate **`WriteTo`** which emits a *fixed-size 16-byte raw block* — used when an outer `Player::WriteReflection` write field 13 needs to dump 9 catalysts inline without per-catalyst bitmaps. **The two encodings are not interchangeable.** Top-level catalyst writes (the 8× catalysts sent alongside the initial LPU) use `WriteReflection`; the inline 9-slot array inside Player.field-13 uses `WriteTo`. See `feedback_writeto_vs_reflection.md` in memory and [Phase 06](../flow/phases/06-spaceship.md).

---

## Quick reference: `WriteReflection` vs `WriteTo`

| Aspect | `WriteReflection(stream)` | `WriteTo(stream)` |
|---|---|---|
| Header | bm1 / bm2 / bmID per FieldCount | none — fixed layout |
| Field selection | by `dataBits.test(i)` | always-all, but field-by-field |
| Size | variable | fixed (compile-time constant) |
| Used for | top-level packet bodies, optional sub-blocks | inline nested arrays inside a reflection field, fixed-size protocol blobs (`ChainData`, `CashOutData`) |
| Inside `reflection_serializer::write<F>(T)` | not directly — that calls `value.WriteTo(...)` for class types | yes — this is the path |

---

## C++ caller checklist

Before adding a new reflection-serialized type:

1. **Pick `FieldCount` carefully** — it sets the regime irreversibly. Going from 8 fields to 9 changes the bitmap from 1 byte to 2 bytes, **breaking every consumer that parses the legacy size**. Bumping past 16 changes the bitmap to per-field IDs — even more breaking.
2. **Order matters for bm1/bm2** — bit `i` writes field `i`. Renumbering fields breaks the wire.
3. **`0xFF` reserved** in bmID. Never assign that as a real field ID.
4. **Class fields delegate to `WriteTo`** — make sure the nested `WriteTo` produces a fixed-size payload, otherwise the reader cannot know where it ends.
5. **Match Read/Write paths.** No automatic schema validation — both sides hand-roll mirror logic.

## C# caller checklist

Same as C++ plus:

6. **Don't forget `WriteBE(ushort)`** for the bm2 regime. `_writer.Write((ushort)x)` defaults to **little-endian** in `System.IO.BinaryWriter` — which would silently break against the C++ peer.
7. **Field index argument** to `ReflectionSerializer.Write(byte field, …)` is a runtime `byte`. There is no `static_assert`. A typo passes `field = 0xFF` and silently emits the terminator mid-stream — **catastrophic**.
8. **No `std::array<T,S>` shortcut.** Loop the elements yourself inside `writeAction`.

---

## Open audit items

1. **No round-trip test.** Neither side has a fuzz / property test comparing C++ `WriteReflection` output bit-for-bit with C# `ReflectionSerializer`. If a regression slipped in (e.g. bm2 written LE), only a live client would notice.
2. **Reader side absent in C#.** `ReflectionSerializer` is write-only. If a future packet uses reflection in the C→S direction, a `ReflectionDeserializer` is required.
3. **`WriteBE` extension surface area.** Confirm `ReCap.Server.Util.BinaryWriterExtensions` covers `uint16`, `uint32`, `uint64`, `int32`, `float`, `double` — at least one of these may be missing.

---

## Files referenced

C++:
- `recap_server_develop/darkspore_server/source/RakNet/Types.h` (`reflection_serializer` template, `bswap`, `Write`/`Read`)
- `recap_server_develop/darkspore_server/source/RakNet/Types.cpp` (instantiations 2, 3, 5, 7, 8, 9, 10, 11, 15, 18, 23)
- `recap_server_develop/darkspore_server/source/Game/Player.cpp` (`WriteReflection<24>`)
- `recap_server_develop/darkspore_server/source/Game/Character.cpp` (`WriteReflection<124>`)
- `recap_server_develop/darkspore_server/source/Game/Object.cpp` (`<2>` interactable, `<3>` loot)

C#:
- `ReCap.Server/Adapters/RakNet/ReflectionSerializer.cs`
- `ReCap.Server/Adapters/RakNet/Packets/LabsPlayerUpdatePacket.cs` (`new ReflectionSerializer(writer, 24)` line 101, `new ReflectionSerializer(writer, 124)` line 210, `new ReflectionSerializer(writer, 2)` line 250)
- `ReCap.Server/Util/BinaryWriterExtensions.cs` (`WriteBE` family)
