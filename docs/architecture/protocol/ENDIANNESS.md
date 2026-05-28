# Endianness â€” the Mixed BE/LE Wire

Darkspore's protocol is **not** uniformly big-endian. The wire mixes BE and LE per-field, decided by which C++ helper wrote the value. Get this wrong and the client either rejects the packet or silently misparses (a worse failure mode â€” see Phase 08 stall history).

Single source of truth for endianness across phase docs. When in doubt, **trace the exact C++ writer and mirror it in C#**.

---

## TL;DR rule

| C++ writer | Endianness on the wire |
|---|---|
| `Write<T>(BitStream&, T)` wrapper (`RakNet/Types.h:217-227`) | **Little-Endian** â€” the wrapper `bswap`s the value, then calls `BitStream::Write<T>` which itself byte-reverses on a little-endian host. Two swaps cancel; wire is LE. (See "wrapper anatomy" below â€” corrected 2026-05-28 from raw packet capture vs the working C++ darkspore_server.) |
| `Write(BitStream&, glm::vec*)` / `Write(BitStream&, glm::quat)` | **Little-Endian** per component (each `f32` goes through the wrapper) |
| Raw `stream.Write<T>(value)` (no wrapper, hits `RakNet::BitStream::Write` directly) | **Big-Endian** on a little-endian host. RakNet 3.902's `BitStream::Write<T>` runs `ReverseBytes` whenever `DoEndianSwap()` is true (default on x86) â€” host LE bytes are reversed to network order on write. This is the path RakNet's own handshake messages take (CONNECTION_REQUEST_ACCEPTED timestamps, etc.). |
| Inline `outStream.Write(PacketID::â€¦)` | **Big-Endian** *as bytes on wire*; for a 1-byte `PacketID` this is moot â€” single byte either way. |
| `RakNet::BitStream::Write<bool>` | **1 byte** (no swap; bool is single-byte) |

> âš ï¸ **Corrected 2026-05-28.** A previous version of this table had the wrapper and raw rows swapped. Raw-capture analysis of the C++ `darkspore_server` (which the retail client loads from) showed the LPU `updateBits`, the `ChainVoteMsgs` value=1 deployment float, the `ChainVoteMsgs` 0x151 blob, and every other field written via the wrapper appear on the wire in LE byte order. The "frozen rule" that `ChainVoteMsgs` 0x151 must be LE is not an exception â€” it is the wrapper's actual behavior. The ReCap C# server has been migrated to LE for darkspore game packets accordingly (commit on 2026-05-28).

The two paths look nearly identical at the call site; the wrapper is in the `RakNet::` namespace and works on primitives, the raw form is `stream.Write<T>` directly. Lexical proximity is the only thing distinguishing them â€” be paranoid.

---

## C++ `Write` wrapper anatomy

Source: `RakNet/Types.h:217-227`.

```cpp
template<typename T>
std::enable_if_t<std::is_integral_v<T> || std::is_floating_point_v<T>, void>
Write(BitStream& stream, const T& value) {
    if constexpr (std::is_same_v<T, bool>) {
        stream.Write<uint8_t>(value);                                  // bool â€” 1 byte, no swap
    } else if constexpr (std::is_integral_v<T> && std::is_signed_v<T>) {
        using Tu = typename std::make_unsigned_t<T>;
        stream.Write<Tu>(bswap<Tu>(static_cast<Tu>(value)));            // signed â†’ unsigned mirror, then bswap
    } else {
        stream.Write<T>(bswap<T>(value));                              // unsigned or floating-point â†’ bswap
    }
}
```

`bswap` (`Types.h:163-199`) is the standard byte-reverse, specialised for `sizeof(T)` 2/4/8 on integers and reinterpret-shuffle on floats. On x86 it compiles down to `bswap` / `bswap rax` / `movbe` instructions; cost is negligible.

`bool` is special: the underlying transport is a single byte, so byte-swap is a no-op.

`uint24_t` is its own thing (`Types.h:234-240`) â€” writes three raw bytes in source order. Used very rarely; treat as **endian-neutral** but verify per-field if it shows up.

`enum` overload (`Types.h:229-232`) delegates to the underlying integral type â†’ goes BE.

### Class types

`Write(BitStream&, T)` with `std::is_class_v<T>` (`Types.h:243+`):

```cpp
template<typename T>
std::enable_if_t<std::is_class_v<T>, void>
Read(BitStream& stream, T& value) {
    if constexpr (std::is_same_v<T, glm::vec2>) { Read(stream, value.x); Read(stream, value.y); }
    // ... etc
}
```

`glm::vec2/vec3/quat` decompose into their float components â€” each component goes through the BE-wrapper. Other class types must define their own `WriteTo` and the endianness is whatever that method picks.

---

## C++ raw `BitStream::Write<T>`

The "no wrapper" path is `stream.Write<T>(value)` (note: method on the stream object, not a free function). `RakNet::BitStream::Write` is the upstream RakNet implementation â€” it **does not byte-swap**. On any little-endian host (x86, ARM in typical config) the wire result is **Little-Endian**.

When this is used:

- `outStream.Write(PacketID::HelloPlayer)` â€” the leading 1-byte opcode (`u8`, swap-neutral anyway, but the principle stands)
- `outStream.Write<uint8_t>(client->mId)` â€” `u8`s never need swap
- Anywhere the C++ ref dev called the raw method directly, deliberately, to keep a payload LE

### Known intentional LE fields

| Field | Why | Reference |
|---|---|---|
| **`ChainVoteMsgs` 0x151-byte buffer** | The body is LE. Flipping to BE breaks the levels/enemies UI. | `feedback_chainvote_le.md`, [Phase 07](../flow/phases/07-chainvote.md), [CLAUDE.md](../../../CLAUDE.md) |
| **`HelloPlayerRequest` UserId** | Client sends LE (standard `BinaryReader` on the C++ side reads via the upstream `BitStream::Read<T>`). | [Phase 06](../flow/phases/06-spaceship.md), `feedback_chainplayermsgs_parse.md` |
| **RakNet meta payloads** (system addresses, GUIDs) | Upstream RakNet framing layer â€” handled inside `RakPeerInterface`, not by Darkspore code. | `Server.cpp:565-595` |

> âš ï¸ **Frozen rule.** Don't flip `ChainVoteMsgs` to BE "to match the rest." Verified breakage multiple times.

---

## C# mirror

Source: `ReCap.Server/Util/BigEndianExtensions.cs`. Provides explicit `WriteBE` / `ReadBE` extensions on top of `System.IO.BinaryReader` / `BinaryWriter`.

```csharp
public static uint  ReadUInt32BE(this BinaryReader reader) { /* 4 bytes, byte-reverse */ }
public static float ReadSingleBE(this BinaryReader reader) { /* â€¦ */ }
public static ushort ReadUInt16BE(this BinaryReader reader) { /* â€¦ */ }
public static ulong  ReadUInt64BE(this BinaryReader reader) { /* â€¦ */ }

public static void WriteBE(this BinaryWriter writer, uint value)   { /* â€¦ */ }
public static void WriteBE(this BinaryWriter writer, ushort value) { /* â€¦ */ }
public static void WriteBE(this BinaryWriter writer, ulong value)  { /* â€¦ */ }
public static void WriteBE(this BinaryWriter writer, int value)    { /* â€¦ */ }
public static void WriteBE(this BinaryWriter writer, byte value)   { /* trivial */ }
public static void WriteBE(this BinaryWriter writer, bool value)   { /* 1 byte */ }
public static void WriteBE(this BinaryWriter writer, float value)  { /* â€¦ */ }
public static void WriteBE(this BinaryWriter writer, Vector2 v)    { WriteBE(X); WriteBE(Y); }
public static void WriteBE(this BinaryWriter writer, Vector3 v)    { WriteBE(X); WriteBE(Y); WriteBE(Z); }
public static void WriteBE(this BinaryWriter writer, Quaternion q) { /* X, Y, Z, W */ }
```

### The C# pitfall

`System.IO.BinaryWriter.Write(ushort)` and friends default to **little-endian**. So:

```csharp
writer.Write((ushort)0x1234);     // wire: 34 12      â€” LE, default behaviour
writer.WriteBE((ushort)0x1234);   // wire: 12 34      â€” BE, our extension
```

Both compile. Both produce no warning. A single character difference between right and wrong. Defensive convention: **on the gameplay wire, prefer `WriteBE` unless the field is explicitly known LE**, and annotate any deliberate `Write(...)` with a `// LE: <why>` comment.

### Two extensions in different namespaces

- `ReCap.Server.Util.BigEndianExtensions` â€” the **gameplay-wire** path used by RakNet packets.
- `ReCap.Server.Adapters.Blaze.Extensions.BinaryWriterExtensions` â€” the **Blaze TDF** path. Different responsibility, different defaults. See [BLAZE_TDF.md](BLAZE_TDF.md).

Don't import them into the same `.cs` file unless you really want naming hell.

---

## Per-field truth table (gameplay wire)

| Phase / packet | Field | Endianness | Source |
|---|---|---|---|
| 06 â€” `HelloPlayerRequest` (Câ†’S) | `UserId` (u64) | **LE** | C++ raw `BitStream::Read` |
| 06 â€” `HelloPlayerRequest` (Câ†’S) | `PlaygroupId` (u32) | **LE** | same |
| 06 â€” `HelloPlayer` (Sâ†’C, ~12 B) | `type`, `mId`, `IP`, `port` | **LE** (`type`, `mId` are 1 byte; `IP` is raw `WriteBits` host bytes; `port` via `Write<uint16_t>` wrapper → LE) | `Server.cpp:1235` |
| 06 â€” `LabsPlayerUpdate` (Sâ†’C) | all primitive fields | BE | `reflection_serializer<24>` â†’ `Write<T>` wrapper |
| 06 â€” `LabsPlayerUpdate` (Sâ†’C) | `bm2` 2-byte bitmap | **LE** | `Write<uint16_t>` wrapper → LE (corrected 2026-05-28) |
| 06 â€” Catalyst inside Player.field 13 | 9Ã— 16-byte raw blocks | per-field, mostly BE | `Catalyst::WriteTo` |
| 07 â€” `ChainVoteMsgs` (Sâ†’C, value=0) | full **0x151-byte buffer** | **LE** (FROZEN) | `ChainData::WriteTo` |
| 07 â€” `ChainVoteMsgs` (Sâ†’C, value=1) | `secondsUntilDeployment` (f32) | **LE** | `Write<float>` wrapper → LE. Capture: `a9 01 00 00 f0 41` = 30.0 LE |
| 08 â€” `ChainPlayerMsgs` (Câ†’S, byteCount=6) | `Value` (u8), `Unknown` (u8) | n/a (1 B) | n/a |
| 08 â€” `ChainPlayerMsgs` (Câ†’S, byteCount=6) | **`SquadId` (u32)** | **LE** | Client `Write<uint32_t>` wrapper → LE (the prior "BE frozen" feedback was based on the inverted doc; capture confirms LE) |
| 08 â€” `GamePrepareForStart` (Sâ†’C, 17 B) | `level`, `markerSet`, `readyMask`, `levelIndex` (4Ã— u32) | BE | `Write<u32>` wrapper |
| 08 â€” `PlayerStatusUpdate` (Câ†’S) | `status` (u32), `progress` (f32) | **LE** | `Read<T>` wrapper → LE |
| 09 â€” `GameStart` (Sâ†’C, 5 B) | `levelIndex` (u32) | **LE** | `Server.cpp:2071-2087` `Write<u32>` wrapper → LE |
| 09 â€” `PlayerCharacterDeploy` (Sâ†’C, 5 B) | `slotId` (u8), `objectId` (u32) | u32 **LE** | `Server.cpp:1421+` via wrapper |
| 10 â€” `GameState` (Sâ†’C) | `gameTime`, `timeElapsed`, `state`, `type`, `_unused` | **LE** | `Server.cpp:1314+` via wrapper |
| 10 â€” `ObjectPlayerMove` (Sâ†’C) | locomotion fields | **LE** | `LocomotionData::WriteTo` via wrapper |
| 11 â€” `ReconnectPlayer` (Sâ†’C, 5 B) | `newState` (u32) | **LE** | `Server.cpp:1268-1277` via wrapper |
| 11 â€” `ChainCashOutMsgs` body (Sâ†’C, 0x2C8) | `mPlanetsCompleted`, `mDna`, medal arrays, chance arrays | BE (default) â€” **unverified** | `Instance.cpp:30-52` |
| 12 â€” `ChainGameMsgs` (Sâ†’C, 2 B) | `state` (u8) | n/a (1 B) | `Server.cpp:2334-2348` |
| 13 â€” `PlayerDeparted` (Sâ†’C, 2 B) | `mId` (u8) | n/a (1 B) | `Server.cpp:1297-1304` |

---

## Audit recipe

When implementing or debugging a packet:

1. Open the C++ writer/reader for the exact field. Note the call: `Write<T>(stream, value)` (free function in `RakNet::` namespace) versus `stream.Write<T>(value)` (raw method on the stream object).
2. Wrapper â‡’ BE. Raw â‡’ LE.
3. Match the C# side with `WriteBE`/`ReadXxxBE` for BE, plain `BinaryWriter.Write`/`BinaryReader.ReadXxx` for LE.
4. If the wire still rejects, hex-diff the bytes against a known-good capture before second-guessing the rule.

---

## Open audit items

1. **`CashOutData` 0x2C8 endianness.** Per the rule, BE. Until a real capture confirms, treat as â“. The ref dev's other 0xâ€¦ buffers vary â€” `ChainData` is LE, this one is presumed BE. See [Phase 11](../flow/phases/11-chaincashout.md).
2. **`uint24_t` byte order.** `Types.h:234-240` writes raw bytes in source order â€” that's "host endianness on whichever side built it." Confirm only-used in BE contexts.
3. **`glm::quat` component order.** C++ writes `(x, y, z, w)`. C# `WriteBE(Quaternion)` should match. Verify the C# extension hasn't accidentally written `(w, x, y, z)`.
4. **C# `WriteBE(Vector3)` reads Y as Z?** Audit the X/Y/Z mapping â€” Unity-style coordinate systems sometimes reorder.

---

## Files referenced

C++:
- `recap_server_develop/darkspore_server/source/RakNet/Types.h` (`bswap`, `Write`/`Read` wrappers, `uint24_t`)

C#:
- `ReCap.Server/Util/BigEndianExtensions.cs` (gameplay-wire `WriteBE`/`ReadXxxBE`)
- `ReCap.Server/Adapters/Blaze/Extensions/BinaryWriterExtensions.cs` (Blaze-TDF path, separate concern)
- `ReCap.Server/Adapters/Blaze/Extensions/BinaryReaderExtensions.cs` (Blaze-TDF path)
- `ReCap.Server/Adapters/RakNet/ReflectionSerializer.cs` (uses `WriteBE` for bm2)
