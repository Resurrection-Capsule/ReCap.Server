# VERIFIED FACTS — the only trusted protocol truths

Every entry here is backed by **evidence**: a C++ `file:line` cite (tree root `C:\CodingProjects\Personal\ReCapCpp\darkspore_server\source`) and/or a wire capture frame. Nothing enters this file on the word of an old doc or memory. If a claim isn't here with a cite, treat it as **unverified** and confirm before relying on it.

Created 2026-05-31 as part of the Phase-0 hard reset (see `docs/superpowers/specs/2026-05-31-port-fidelity-plan-design.md`). Replaces the deleted dogma docs (`PARITY.md`, `ENDIANNESS.md`) and the CLAUDE.md FROZEN section.

---

## Wire encoding

- **`Write<T>` wrapper → little-endian on the wire.** The C++ `Write<T>(BitStream&, T)` wrapper (`RakNet/Types.h:218-226`) bswaps once, then `BitStream::Write<T>` byte-reverses again on an LE host — two swaps cancel, wire is **LE**. Verified 2026-05-28 by raw capture vs the working C++ server, re-confirmed 2026-05-31 (LPU/GameState/deploy all LE on the wire). Darkspore game packets are LE.
  - Corollary: the old "Write<T> = WriteBE" rule is **wrong** (see superseded list).
  - **Why the second swap actually happens (settles a recurring re-litigation):** `Server.h:20` *intends* `#define __BITSTREAM_NATIVE_END` (which would disable RakNet's swap and yield BE). But it is a **no-op due to header ordering**: `Server.cpp:3` includes `Server.h` first; `Server.h:9` includes `Types.h`; `Types.h:11` includes `<BitStream.h>` **before** the `#define` at `Server.h:20`. The include guard compiles BitStream.h's body once — at that first include the macro is undefined — so `DoEndianSwap()` is compiled to **TRUE** (RakNet swaps) and the later `#define` never takes effect. Net: the legacy double-swap path is what's compiled → **wire = LE**. A source-only read of `Server.h:20` looks like BE; the include order defeats it. Verified by reading `Server.cpp:3`, `Server.h:9/20`, `Types.h:11` directly (2026-05-31).
- **ReflectionSerializer bitmap sizes** (C# `Adapters/RakNet/ReflectionSerializer.cs`, matches C++): ≤8 fields → 1-byte bitmap; 9–16 → 2-byte bitmap; >16 → field-ID byte per field + `0xFF` terminator. Confirmed on wire: ObjectCreate createData (10 fields → 2-byte bitmap), Object/Character reflections (>16 → field-ID/0xFF).
- **WriteTo vs WriteReflection:** nested class arrays inside a reflection block use raw fixed-size `WriteTo()`; top-level standalone blocks use `WriteReflection()`. Confirmed: Player reflection field 3 = 3× `Character::WriteTo` (raw 0x620).

## RakNet transport (RakNexus)

- **RakNexus delivers correctly.** Wire capture 2026-05-31: sequential datagram numbers, client ACKs every datagram, split-packet LPU reassembled, client acts (state transitions, deploys). The transport is NOT a source of gameplay bugs.
- Only framing divergence vs real RakNet: datagram flag byte `0x80` (C#) vs `0x84` (C++) — the `0x04` bit is RakNet's optional B&AS congestion-control values, which RakNexus omits. Harmless; valid datagram.

## Character block — `Character::WriteTo` (Game/Character.cpp:99)

Fixed **0x620** (1568) byte block, all fields via `Write<T>` → LE. Verified offsets (C# `LabsCharacterData.WriteTo` matches):
- `0x000–0x007`: zero (not written)
- `0x008`: u64 mAssetId · `0x010`: i32 mVersion
- `0x0B4`: u32 mNounId
- `0x0B8`: f32[] mPartAttributes, **base 0x0B8, stride 4** per AttributeType index (e.g. MaxHealth idx4 @0x0C8, AttackSpeedScale idx0x17 @0x114, MinWeaponDamage idx0x65 @0x258)
- `0x3B8`: u32 mCreatureType
- `0x3C0`: u64 mDeployCooldown · `0x3C8`: u32 mAbilityPoints · `0x3CC`: u32[9] ability ranks
- `0x3F0`: f32 Health, MaxHealth, Mana, MaxMana, GearScore, GearScoreFlattened
- Gaps contain whatever (C++ leaves uninitialized heap there — pointer garbage on the wire); the client only reads the fields it cares about, so gap content is irrelevant.

## LabsPlayerUpdate / dataBits

- **C++ ships 3 (default) Character blocks in the HelloPlayer LPU** via player dataBit 3 (set in `Player` ctor, `Player.cpp:64`); `Player::WriteReflection` field 3 emits 3× `Character::WriteTo`. The 3 `Character` objects exist from `std::array<Character,3>` construction (default/zero values; no squad yet at hello).
- **Adding dataBit 3 does NOT break chain vote.** Tested 2026-05-31: with bit 3 in `SetInitialDataBits`, hello LPU = 4818B with chars, and the client still sent `ChainPlayerMsgs(byteCount=6)` (the vote). Directly disproves the old FROZEN claim. (Current C# `SetInitialDataBits` = `{0,3,4,5,6,7,8,12,15,16,18,21,22}`.)

## Client deck HUD (Darkspore.exe in Ghidra, base 0x400000)

- The in-game deck HUD `cPlayerDeck::UpdateHud` (@0x51b860, runs every frame via the UI update `FUN_007ee9d0`) reads creatures from the **live game-state singleton** `DAT_0143ffd8+0x710` (creature vector begin@+0x10/end@+0x14, stride 0x40), pushing per-creature fields (e.g. "gearScore") to Scaleform. It is **game-state/LPU-driven, NOT REST** (REST account data is menu/editor-side).
- **Open crash (not yet fixed):** `MOV ECX,[ESI+0x20]; CALL 0x551f10` — `this = cPlayerDeck+0x20` (the Scaleform GFx movie handle) is NULL → ACCESS_VIOLATION read 0x0 @ 0x00551f47. The deck HUD movie never binds. Insensitive to the gameplay-packet changes tried so far (ObjectCreate, hello-chars). FUN_00551f10 = GFx invoke helper.

## To re-verify before trusting (carried over, NOT yet confirmed this cycle)

These were asserted by old docs; keep until verified, then move up with a cite or kill:
- `ChainVoteMsgs` 0x151 buffer = LE (consistent with the Write<T>=LE fact, but not independently re-captured this cycle).
- `HelloPlayer` (0x80) body = 8 bytes `u8 type, u8 gameplayIndex, u32 IPv4, u16 Port` (claimed via Ghidra `FUN_00a93d50`).
- `GameType = 0` in the update loop; `LabsPlayerData.DataSetup = false`.
- Wire state codes: Spaceship 0x02, PreDungeon 0x05, Dungeon 0x06, ChainVoting 0x0B, ChainCashOut 0x0C.

---

## Superseded "laws" changelog

Claims removed in the Phase-0 reset, with the evidence that killed or demoted them:

| Old "law" | Source | Verdict | Evidence |
|---|---|---|---|
| `Write<T>` wrapper = **Big-Endian** ("most game data BE") | CLAUDE.md, PORTING_PLAN #5, ENDIANNESS.md (pre-correction) | **WRONG → LE** | Raw capture vs working C++ server (2026-05-28); LPU/GameState/deploy all LE on wire (2026-05-31). |
| `SetInitialDataBits` MUST be 12 bits; adding `{3,13,14,17}` breaks chain vote — "FROZEN, never change" | CLAUDE.md FROZEN, PARITY.md | **DISPROVEN for bit 3** | Added bit 3 → vote still fired (`ChainPlayerMsgs` byteCount=6), hello LPU 4818B (2026-05-31). Was likely a malformed-Character-block artifact, since fixed. (13/14/17 not yet tested.) |
| "Follow C++ reference religiously" / "Frozen rules inviolable" | CLAUDE.md, PORTING_PLAN #3 | **Demoted** | Replaced by evidence-over-dogma + "match the client's required contract, not C++ byte-garbage". |

When a future finding kills another old claim, add a row here.
