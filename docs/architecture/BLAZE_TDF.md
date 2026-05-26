# Blaze TDF (Tagged Data Format)

EA Blaze's wire format for all Login/Lobby/UserSessions/GameManager payloads. Used by Phases 01 (Redirector), 02 (Auth), 04 (GameManager). Independent of the gameplay-wire (RakNet/UDP) format documented in [ENDIANNESS.md](ENDIANNESS.md).

This page is the single source of truth for TDF encoding. Phase docs link here.

---

## Frame on the wire

Every Blaze message:

```
+------+------+------+------+------+------+--------+------------------+
|len16 |comp16|cmd16 | err16|qtype |  id  |ext16?  |  TDF body…       |
+------+------+------+------+------+------+--------+------------------+
   2B    2B    2B    2B    2B    2B    2B?         len bytes
```

Header is **8× u16 little-endian** (always — `DataBuffer::read_u16_le` in `Blaze/Packet.cpp:75-85`). Optional `ext16` (`mExtLength`) is present iff `qtype & 0x10`. Field meanings:

| Field | Width | Notes |
|---|---|---|
| `len` | u16 LE | TDF body length (excluding header). |
| `component` | u16 LE | Component ID (`0x0001` Auth, `0x0004` GameManager, `0x0007` Redirector, …). See [FLOW_CPP.md](FLOW_CPP.md) for the table. |
| `command` | u16 LE | Method ordinal within the component (e.g. `Auth::login = 0x28`). |
| `error` | u16 LE | Reply error code; `0` on success. |
| `qtype` | u16 LE | Bit field: bit `0x10` = "has ext length"; other bits = request/reply/notify discriminator. |
| `id` | u16 LE | Round-trip ID — replies echo the request's `id`. |
| `extLength` | u16 LE | Optional; total body length is `(extLength << 16) | len` when present. |

The TDF body follows immediately. **No framing inside the body** — fields self-describe via tags and type codes.

---

## TDF body anatomy

Each TDF field is **4 bytes of header** followed by the payload.

```
+--------+--------+--------+-----+
|  tagA  |  tagB  |  tagC  |type |
+--------+--------+--------+-----+
   1B       1B       1B    1B (low 5 bits)
```

- The **tag** is a 4-character ASCII label compressed into 24 bits — see "Label encoding" below.
- The **type** is the low 5 bits of byte 4. High 3 bits are reserved (some Blaze variants use them as flags; in Darkspore they are always zero).

### Label encoding

ASCII labels A–Z (and `' '` to `_`) encoded 6 bits per character into a 24-bit field. C# implementation in `Tdf.LabelToTag` (`Adapters/Blaze/Tdf.cs:486-499`):

```csharp
public static uint LabelToTag(string label)
{
    var result = 0u;
    for (var i = 0; i < Math.Min(label.Length, 4); ++i)
    {
        if (label[i] < ' ' || label[i] > '_')
            throw new ArgumentOutOfRangeException(nameof(label), "Label only allows ' ' to '_'");
        result |= (uint)((label[i] - 0x20) << (8 + (3 - i) * 6));
    }
    return result;
}
```

And the inverse `TagToLabel` (`Tdf.cs:503-518`) shifts back, treating spaces as padding.

C++ does the same via `Network::CompressLabel` / `DecompressLabel` (`Blaze/Packet.cpp:48-68`):

```cpp
uint32_t CompressLabel(const std::string& label) {
    uint32_t ret = 0;
    for (int i = 0; i < 4; ++i) {
        ret |= (0x20 | (label[i] & 0x1F)) << ((3 - i) * 6);
    }
    return bswap32(ret) >> 8;
}
```

Examples:

| Label | Tag (hex) |
|---|---|
| `"AMAP"` | `0x82DA1080` (see `RedirectorComponent.cpp:148`) |
| `"VALU"` | `0xD8C2EA80` |
| `"NMAP"` | (compute via `LabelToTag`) |
| 3-char labels (`"PID"`) | last char position padded with `0x20` (space) |

> 4-char labels are conventional but 1–4 chars work; shorter labels space-pad. **The 4th char is encoded but `TagToLabel` strips trailing spaces** — so `"PID "` and `"PID"` round-trip to `"PID"`.

### Type codes

Source: `Adapters/Blaze/Tdf.cs:9-23` (canonical C# enum) + `Blaze/TDF.h:27-41` (C++ enum):

| Code | Name | Payload |
|---|---|---|
| `0x0` | Integer | Variable-length signed (see "Varint" below). |
| `0x1` | String | Varint length-prefix `(len+1)`, raw bytes, **trailing NUL**. |
| `0x2` | Binary | Varint length-prefix, raw bytes — no NUL. |
| `0x3` | Struct | Nested TDF fields until `0x00` byte terminator. |
| `0x4` | List | Varint type-tag, varint count, then N payloads. |
| `0x5` | Map | Varint key-type, varint value-type, varint count, then N (key,value) pairs. |
| `0x6` | Union | Varint active-member-tag + nested struct, or sentinel `0x7F` for "no member". |
| `0x7` | Variable | Polymorphic — `0x01` byte, then `<type><payload>`, or `0x00` for null. |
| `0x8` | BlazeObjectType | `(u16 component, u16 type)` — varint pair. |
| `0x9` | BlazeObjectId | `(u16, u16, u64)` triple — varint each. |
| `0xA` | Float | 4 bytes IEEE-754, **big-endian** on the wire. |
| `0xB` | TimeValue | 64-bit timestamp; varint encoded. |

Codes > `0xB` are invalid; `0xFF` (`Type::Invalid` in C++) is a sentinel.

---

## Varint encoding (Blaze's "compressed integer")

Used for: all `Integer` payloads, list/map counts, string length prefixes, union tags. **Not** related to protobuf varint — Blaze's scheme is custom.

Source: `Blaze/Packet.cpp:146-176` (C++), `Adapters/Blaze/TdfEncoder.cs:429-455` (C#).

### Encode

```csharp
private void EncodeVarsizeInteger(long value)
{
    if (value == 0) { Writer.Write((byte)0); return; }

    byte negativeBit = 0;
    if (value < 0) { negativeBit = 0x40; value = -value; }

    // First byte: low 6 bits + sign bit (0x40) + continue bit (0x80)
    Writer.Write((byte)((value & 0x3F) | negativeBit | (value >= 0x40 ? 0x80 : 0x00)));
    value >>= 6;

    // Subsequent bytes: low 7 bits + continue bit (0x80)
    while (value > 0)
    {
        Writer.Write((byte)((value & 0x7F) | (value >= 0x80 ? 0x80 : 0x00)));
        value >>= 7;
    }
}
```

### Decode (C++ `decode_integer`, `Packet.cpp:162-176`)

```cpp
uint32_t Packet::decode_integer() {
    uint32_t value = mStream.read<uint8_t>();
    if (value >= 0x80) {
        value &= 0x3F;
        for (uint32_t i = 1; i < 8; i++) {
            uint32_t next_value = mStream.read<uint8_t>();
            value |= (next_value & 0x7F) << ((i * 7) - 1);
            if (next_value < 0x80) break;
        }
    }
    return value;
}
```

> ⚠️ **Asymmetry alert.** The C++ decoder uses **`uint32_t`** and drops the negative bit. The C# encoder handles signed `long` and tracks negativity via the `0x40` bit. If a payload ever carries a negative integer through the C++ side it will be misparsed. Verify per call site before assuming sign handling.

### Byte layout

| Byte | Bits | Meaning |
|---|---|---|
| #0 | `0x80` | Continuation: more bytes follow |
| #0 | `0x40` | Sign: 1 = negative |
| #0 | `0x3F` | Low 6 bits of magnitude |
| #1+ | `0x80` | Continuation |
| #1+ | `0x7F` | Next 7 bits of magnitude |

Worst case: ulong64 = 10 bytes (1× 6-bit chunk + 9× 7-bit chunks). Best case: zero/small positive = 1 byte.

---

## Per-type encoding

### Integer (type `0x0`)

```
<tag 3B> <type=0x00>  <varint value>
```

C# encoder: `TdfEncoder.EncodeUInt64` (`TdfEncoder.cs:289+`) and friends. C++ encoder: `Packet::write_integer` → `encode_integer` (`Packet.cpp:146-160`).

### String (type `0x1`)

```
<tag 3B> <type=0x01>  <varint (len+1)>  <raw bytes…>  <0x00>
```

Length prefix includes the trailing NUL byte (hence `+1`). Empty string: prefix `0x01`, then a lone `0x00`. C++: `Packet::write_string` (`Packet.cpp:136-144`); C#: `TdfEncoder.EncodeString` (`TdfEncoder.cs:135+`).

### Binary (type `0x2`)

```
<tag 3B> <type=0x02>  <varint length>  <raw bytes…>
```

Same as String but **no trailing NUL** and length is the exact byte count.

### Struct (type `0x3`)

```
<tag 3B> <type=0x03>  <nested fields…>  <0x00>
```

The terminator is a single `0x00` byte where the next field's tag would start. Nested structs nest indefinitely.

> ⚠️ A field tag `0x000000NN` (label starting with space) would clash with the struct terminator. Blaze's label validator (`Tdf.cs:492`) rejects characters below `' '`, so the leading byte is always ≥ `0x20`. Safe by construction.

### List (type `0x4`)

```
<tag 3B> <type=0x04>  <varint elementType>  <varint count>  <element payloads…>
```

`elementType` is a TDF type code (0..0xB). Element payloads carry **no tag header** — they are bare values back-to-back.

Special-case stub list: `push_list("LABEL", Type, isStub=true)` writes an empty list — count = 0, no elements. Used to advertise a field exists without populating it.

### Map (type `0x5`)

```
<tag 3B> <type=0x05>  <varint keyType>  <varint valueType>  <varint count>
                       <key0 payload>  <value0 payload>
                       <key1 payload>  <value1 payload>  …
```

Each (k, v) is bare — no per-entry tags.

### Union (type `0x6`)

```
<tag 3B> <type=0x06>  <varint activeMember>  <nested struct…>
```

Or `<varint 0x7F>` for "no member set" (`TdfUnion.Decode` in `Tdf.cs:600-615`). Active-member index selects which struct variant follows.

Used heavily by `NetworkAddress` (IPv4 vs IPv6 vs XBOX address — see `RedirectorComponent::WriteServerAddressInfo` `RedirectorComponent.cpp:179+`).

### Variable (type `0x7`)

```
<tag 3B> <type=0x07>  <0x01>  <typeCode 1B>  <payload>
```

Or `<0x00>` for null. Allows polymorphic fields — one of the few places type info is on the wire.

### BlazeObjectType (`0x8`) / BlazeObjectId (`0x9`)

Fixed-shape tuples:

```
ObjectType: <varint component> <varint type>                    // (u16, u16)
ObjectId:   <varint component> <varint type> <varint instance>  // (u16, u16, u64)
```

### Float (`0xA`)

```
<tag 3B> <type=0x0A>  <4 bytes IEEE-754 BE>
```

> Note: **Float is big-endian** even though the surrounding Blaze frame header is LE. The TDF body endian mix mirrors the gameplay wire — see [ENDIANNESS.md](ENDIANNESS.md).

### TimeValue (`0xB`)

```
<tag 3B> <type=0x0B>  <varint timestamp>
```

64-bit (Blaze-defined epoch — confirm semantics from a real capture).

---

## C# class model

`ReCap.Server/Adapters/Blaze/`:

| File | Role |
|---|---|
| `Tdf.cs` | `TdfType` enum, `TdfFieldType` enum, `[TdfFieldAttribute]` for declarative schema, base `Tdf` class with reflection-driven `Encode`/`Decode`, `LabelToTag`/`TagToLabel`. |
| `TdfEncoder.cs` | `BinaryWriter` wrapper with `EncodeInteger`/`EncodeString`/`EncodeStruct`/… per-type methods. `PutHeader(tag, type)` writes the 4-byte field header. |
| `TdfDecoder.cs` | Mirror reader. |
| `TdfMap.cs` | Generic `TdfMap<K, V>` collection. |
| `TdfVector.cs` | Generic `TdfVector<T>` for typed lists (`TdfPrimitiveVector<T>`, `TdfStructVector<T>`). |
| `Packet.cs` | The 14-byte (or 16-byte w/ ext) Blaze frame header reader/writer. |

Typical Blaze message class:

```csharp
public class LoginRequest : Tdf
{
    [TdfField("MAIL")] public string Email { get; set; } = "";
    [TdfField("PASS")] public string Password { get; set; } = "";
    [TdfField("PNAM")] public string PersonaName { get; set; } = "";
    // base Tdf.Encode iterates [TdfField] properties via reflection
}
```

The `[TdfField("MAIL")]` attribute carries the 4-char label; the property's CLR type drives the dispatch (string ⇒ `EncodeString`, primitive numerics ⇒ `EncodeUInt32`/etc., nested `Tdf` ⇒ `EncodeStruct`, `TdfMap<K,V>` ⇒ `EncodeMap`, etc.).

---

## Component dispatch

The 14-byte header carries `(component, command, qtype, id)`. Dispatch is two-level:

1. **Component lookup** — `Adapters/Blaze/BlazeServer.cs` registers each `IComponent` by ID (`AuthenticationComponent` = `0x0001`, `GameManagerComponent` = `0x0004`, etc.).
2. **Command dispatch** — `IComponent.Handle(command, packet)` routes by command ordinal. Each component declares its own command enum (e.g. `AuthCommand.Login = 0x28`).

Replies echo the request's `id` and clear `qtype`'s "is-notify" bit. Notifications carry a fresh `id` and have a different `qtype` discriminator (verify exact bit per a wire capture — Blaze docs are inconsistent).

See [Phase 01](phases/01-redirector.md), [Phase 02](phases/02-blaze-auth.md), [Phase 04](phases/04-blaze-gamemanager.md) for the concrete component/command tables.

---

## Common pitfalls

1. **LE header, BE-ish body.** Frame header (`len`, `component`, `command`, …) is u16 LE. Floats inside the body are BE. Integers are varint (sign- and length-aware). String length is varint, *not* a fixed u16. Easy to mix up.
2. **String length includes NUL.** Encode `"hi"` (2 chars) as varint `3` followed by `hi\0`. Off-by-one bugs here silently corrupt the next field.
3. **Struct terminator vs field-tag confusion.** A struct ends on a single `0x00` byte. If you forget to emit it, the parent struct keeps reading into the next sibling. If you emit it twice, the parent stops short. The encoder API hides this — `EncodeStruct` writes the terminator on close — but hand-crafted bytes will trip.
4. **Variable type discriminator.** `0x01` followed by `<typeCode><payload>` for "set"; `0x00` for "null." Not a regular type code in the outer header position.
5. **Negative integers via 0x40 sign bit.** C++ decoder doesn't honor it. C# does. Avoid signed varints across the wire until both sides agree.
6. **`LabelToTag` only accepts `' '` (0x20) through `'_'` (0x5F).** Lowercase letters throw `ArgumentOutOfRangeException`. All Blaze labels are uppercase + digits + `_`.
7. **Tag is `(label << 8) | typeCode`** packed into one u32, written big-endian first 3 bytes + 1 byte type. C# `PutHeader` (`TdfEncoder.cs:421-427`) emits in the order `byte0..byte2` of the shifted-left tag, then `type & 0x1F`.
8. **`extLength` for big payloads.** If body > 64 KiB, `qtype |= 0x10` and an extra u16 length appears at byte offset 14. Most Darkspore payloads stay below 64 KiB; the path is rarely exercised. Verify before relying.

---

## Parity table

| Item | C++ | C# | Status |
|---|---|---|---|
| Frame header (8× u16 LE) | `Packet::Packet` (`Packet.cpp:71-86`) | `Adapters/Blaze/Packet.cs` | ✅ |
| `extLength` for >64 KiB | `Packet.cpp:81-85` | confirm wired | ❓ |
| Label compression (24-bit, 6 bits/char, ' ' base) | `CompressLabel` (`Packet.cpp:48-54`) | `LabelToTag` (`Tdf.cs:486-499`) | ✅ |
| `TagToLabel` reverse | `DecompressLabel` (`Packet.cpp:56-68`) | `TagToLabel` (`Tdf.cs:503-518`) | ✅ |
| Varint encode | `encode_integer` (`Packet.cpp:146-160`) | `EncodeVarsizeInteger` (`TdfEncoder.cs:429-455`) | ⚠️ |
| Varint sign bit | C# emits `0x40`; C++ decoder ignores | mismatch | ⚠️ |
| Integer type (0x0) | yes | yes | ✅ |
| String type (0x1, NUL-terminated, varint length+1) | `write_string` (`Packet.cpp:136-144`) | `EncodeString` (`TdfEncoder.cs:135+`) | ✅ |
| Binary type (0x2) | yes | `EncodeBinary` (`TdfEncoder.cs:124-133`) | ✅ |
| Struct type (0x3) + `0x00` terminator | yes | yes | ✅ |
| List type (0x4) | yes (`push_list`) | `EncodeList` (`TdfEncoder.cs:407+`) | ✅ |
| Map type (0x5) | yes (`push_map`) | `EncodeMap` (`TdfEncoder.cs:385+`) | ✅ |
| Union type (0x6) | yes (`push_union`) | `EncodeUnion` (`TdfEncoder.cs:97+`) | ✅ |
| Variable type (0x7) | yes | `EncodeVariable` (`TdfEncoder.cs:76+`) | ✅ |
| BlazeObjectType (0x8) | `object_type = tuple<u16,u16>` (`TDF.h:16`) | `BlazeObjectType` struct | ✅ |
| BlazeObjectId (0x9) | `object_id = tuple<u16,u16,u64>` (`TDF.h:17`) | `BlazeObjectId` struct | ✅ |
| Float (0xA, BE) | `Type::Float` declared (`TDF.h:38`) | `EncodeFloat` (`TdfEncoder.cs:161+`) | ❓ — confirm BE on C++ wire writer |
| TimeValue (0xB) | yes | `EncodeTimeValue` (`TdfEncoder.cs:46+`) | ✅ |

---

## Open audit items

1. **Negative-integer parity.** C++ `decode_integer` returns `uint32_t` and drops the `0x40` sign bit. Decide: either change C# to never emit the sign bit, or fix C++ to honor it. Doing nothing leaves a latent bug.
2. **Capture a real `qtype` table.** The bit semantics (notify vs request vs reply, has-ext-length, has-context, etc.) are folklore. A real session capture would settle it.
3. **`Float` byte order on C++ side.** The C++ TDF header declares `Float = 0xA` but the `WriteFloat` helper has not been audited in detail. Confirm it writes BE.
4. **`extLength` round-trip.** Send a >64 KiB payload (e.g. a 100-entry Map of struct values) and confirm both sides agree on the framing.
5. **`Tdf` reflection performance.** C# `Tdf.Encode` walks properties via `Type.GetProperties()` every send. Profile under load — pre-cache the per-type encoder if the lobby phase ever feels slow.
6. **Unicode in strings.** The label encoder rejects non-ASCII. Verify the *value* encoder writes UTF-8 (default for `BinaryWriter` with `Encoding.UTF8`) and that the client side accepts it.

---

## Files referenced

C++:
- `recap_server_develop/darkspore_server/source/Blaze/TDF.h` (`enum Type`, `Header`, `Packet`, `Parser`)
- `recap_server_develop/darkspore_server/source/Blaze/Packet.cpp` (`CompressLabel`, `DecompressLabel`, `Packet` ctor, `encode_integer`/`decode_integer`, `write_string`/`read_string`)
- `recap_server_develop/darkspore_server/source/Blaze/Component/RedirectorComponent.cpp` (concrete `push_list`/`push_struct` usage, `WriteServerAddressInfo`)
- `recap_server_develop/darkspore_server/source/Blaze/Component/UtilComponent.cpp` (`WritePostAuth` etc.)

C#:
- `ReCap.Server/Adapters/Blaze/Tdf.cs` (`TdfType` enum, `LabelToTag`, base `Tdf` class)
- `ReCap.Server/Adapters/Blaze/TdfEncoder.cs` (`PutHeader`, `EncodeVarsizeInteger`, per-type encoders)
- `ReCap.Server/Adapters/Blaze/TdfDecoder.cs`
- `ReCap.Server/Adapters/Blaze/TdfMap.cs`
- `ReCap.Server/Adapters/Blaze/TdfVector.cs`
- `ReCap.Server/Adapters/Blaze/Packet.cs` (frame header)
- `ReCap.Server/Adapters/Blaze/BlazeServer.cs` (component registration)
