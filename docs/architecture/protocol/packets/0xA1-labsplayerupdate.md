# 0xA1 — LabsPlayerUpdate

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | variable | [06 Spaceship](../../flow/phases/06-spaceship.md), [08 PreDungeon](../../flow/phases/08-predungeon.md), [10 Dungeon loop](../../flow/phases/10-gameloop.md) | ⚠️ dataBits updated 2026-05-31; rest ✅ |

The central player-state synchronization packet. Sent by the server whenever `updateBits != 0`: on first join, after `SetSquad`, on every `PlayerStatusUpdate`, and every 50 ms tick if anything changed. The body is sparse — only fields with set bits are written. Size varies from ~5 bytes (header only, rare) to several kilobytes (full initial burst with all catalysts + characters).

> **Updated 2026-05-31:** The initial `dataBits` set in C# is now `{0,3,4,5,6,7,8,12,15,16,18,21,22}` (13 bits — bit 3 re-added). C++ sets 16 bits including `{3,13,14,17}`. Bit 3 is safe — vote still fires. Adding `{13,14,17}` not yet tested. See [VERIFIED_FACTS.md](../../VERIFIED_FACTS.md) and [Phase 06](../../flow/phases/06-spaceship.md).

> ⚠️ SUPERSEDED 2026-05-31 — see VERIFIED_FACTS.md (FROZEN bit-3 rule disproved; bit 3 is safe)

---

## Packet header

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `PlayerId` | u8 | n/a | `Write<uint8_t>` BE wrapper — 1-byte, no swap. Client slot index (0-based). |
| `0x01` | `UpdateBits` | u16 | **BE** | `Write<uint16_t>` BE wrapper. Bitmask selecting which sub-sections follow. |

Total header: 1 opcode + 1 PlayerId + 2 UpdateBits = **4 bytes** before any payload.

---

## UpdateBits layout

Defined in `RakNet/Types.h:123-149`:

| Bit(s) | Mask | Name | Payload when set |
|---|---|---|---|
| 0 | `0x0001` | `CharacterLeft` | Character[0] `WriteReflection` |
| 1 | `0x0002` | `CharacterCenter` | Character[1] `WriteReflection` |
| 2 | `0x0004` | `CharacterRight` | Character[2] `WriteReflection` |
| 0–2 | `0x0007` | `CharacterMask` | All 3 characters (whichever bits set) |
| 3 | `0x0008` | `CrystalTopLeft` | Catalyst[0] `WriteReflection` |
| 4 | `0x0010` | `CrystalTopCenter` | Catalyst[1] |
| 5 | `0x0020` | `CrystalTopRight` | Catalyst[2] |
| 6 | `0x0040` | `CrystalMidLeft` | Catalyst[3] |
| 7 | `0x0080` | `CrystalMidCenter` | Catalyst[4] |
| 8 | `0x0100` | `CrystalMidRight` | Catalyst[5] |
| 9 | `0x0200` | `CrystalBottomLeft` | Catalyst[6] |
| 10 | `0x0400` | `CrystalBottomCenter` | Catalyst[7] |
| 11 | `0x0800` | `CrystalBottomRight` | Catalyst[8] |
| 3–11 | `0x0FF8` | `CrystalMask` | All 9 crystals (whichever bits set) |
| 12 | `0x1000` | `PlayerBits` | Player `WriteReflection` block |

The order written to the wire: **Player reflection first**, then per-character reflections (indices 0→2), then per-crystal reflections (indices 0→8). This is the C++ order (`updateBits & PlayerBits` → `CharacterMask` → `CrystalMask`).

> ⚠️ **C# order differs from C++.** `LabsPlayerUpdatePacket.WriteTo` writes Player → Characters → Crystals which matches C++. However, the C# loop checks `CharacterMask` before `CrystalMask`, identical to C++. Order is correct.

---

## Player reflection (`reflection_serializer<24>`)

Written when `updateBits & PlayerBits`. Uses `reflection_serializer<24>` → field-count > 16 → byte field-ID per field + `0xFF` terminator. See [REFLECTION_SERIALIZER.md](../REFLECTION_SERIALIZER.md).

Only fields whose `dataBit` is set are written:

| Field ID | C++ field | C# property | Type | Endian | Notes |
|---|---|---|---|---|---|
| 0 | `mbDataSetup` | `DataSetup` | bool | n/a | `false` in C# — `true` = demo/capped mode, causes bugs |
| 1 | `mCurrentDeckIndex` | `CurrentDeckIndex` | i32 | **BE** | |
| 2 | `mQueuedDeckIndex` | `QueuedDeckIndex` | i32 | **BE** | |
| 3 | `mCharacterData` | `Characters[3]` | blob | — | 3× `Character::WriteTo` = 3 × 0x620 bytes (raw fixed, NOT WriteReflection) |
| 4 | `mPlayerIndex` | `PlayerIndex` | u8 | n/a | |
| 5 | `mTeam` | `Team` | u8 | n/a | Default 1 |
| 6 | `mPlayerOnlineId` | `PlayerOnlineId` | u64 | **BE** | |
| 7 | `mStatus` | `Status` | u32 | **BE** | |
| 8 | `mStatusProgress` | `StatusProgress` | f32 | **BE** | |
| 9 | `mCurrentCreatureId` | `CurrentCreatureId` | u32 | **BE** | ❓ not in C# `SetInitialDataBits` or standard write paths |
| 10 | `mEnergyPoints` | `EnergyPoints` | f32 | **BE** | ❓ not in C# initial bits |
| 11 | `mbIsCharged` | `IsCharged` | bool | n/a | ❓ not in C# initial bits |
| 12 | `mDNA` | `DNA` | i32 | **BE** | |
| 13 | `mCatalysts` | `Catalysts[9]` | blob | — | 9× `Catalyst::WriteTo` = 9 × 16 bytes (raw fixed) |
| 14 | `mCatalystBonuses` | `CatalystBonuses[8]` | bool[8] | n/a | |
| 15 | `mAvatarLevel` | `AvatarLevel` | u32 | **BE** | |
| 16 | `mAvatarXP` | `AvatarXP` | f32 | **BE** | |
| 17 | `mChainProgression` | `ChainProgression` | u32 | **BE** | 🔒 excluded from initial dataBits |
| 18 | `mLockCamera` | `LockCamera` | bool | n/a | |
| 19 | `mbLockedOverdrive` | `LockedOverdrive` | bool | n/a | |
| 20 | `mbLockedCrystals` | `LockedCrystals` | bool | n/a | |
| 21 | `mLockedAbilityMin` | `LockedAbilityMin` | u32 | **BE** | Default `0xFF` |
| 22 | `mLockedDeckIndexMin` | `LockedDeckIndexMin` | u32 | **BE** | Default `0xFF` |
| 23 | `mDeckScore` | `DeckScore` | u32 | **BE** | |

**C# initial dataBits (as of 2026-05-31):** `{0, 3, 4, 5, 6, 7, 8, 12, 15, 16, 18, 21, 22}` — 13 fields (bit 3 re-added).

**C++ initial dataBits:** `{0, 3, 4, 5, 6, 7, 8, 12, 13, 14, 15, 16, 17, 18, 21, 22}` — 16 fields.

Still missing from C# vs C++: `{13=Catalysts, 14=CatalystBonuses, 17=ChainProgression}` — not yet tested safe.

> ⚠️ SUPERSEDED 2026-05-31 — see VERIFIED_FACTS.md (FROZEN label removed; bit 3 now in C# initial set)

---

## Character reflection (`reflection_serializer<124>`)

Written per-character when `updateBits & (CharacterBits << i)`. Uses `reflection_serializer<124>` → field-ID byte per field + `0xFF` terminator. **All 13 fields written on first burst** (ctor sets all bits).

| Field ID | C++ field | Type | Endian |
|---|---|---|---|
| 0 | `mVersion` | i32 | **BE** |
| 1 | `mNounId` | u32 | **BE** |
| 2 | `mAssetId` | u64 | **BE** |
| 3 | `mCreatureType` | u32 | **BE** |
| 4 | `mDeployCooldown` | u64 | **BE** |
| 5 | `mAbilityPoints` | u32 | **BE** |
| 6 | `mAbilityRanks[9]` | u32[9] | **BE** each |
| 7 | `mHealth` | f32 | **BE** |
| 8 | `mMaxHealth` | f32 | **BE** |
| 9 | `mMana` | f32 | **BE** |
| 10 | `mMaxMana` | f32 | **BE** |
| 11 | `mGearScore` | f32 | **BE** |
| 12 | `mGearScoreFlattened` | f32 | **BE** |

Character `WriteTo` (field 3 of Player reflection) emits a **0x620-byte fixed-size raw block** — not the `WriteReflection` form. See [REFLECTION_SERIALIZER.md](../REFLECTION_SERIALIZER.md) "WriteTo vs WriteReflection" rule.

---

## Catalyst reflection (`reflection_serializer<2>`)

Written per-catalyst when `updateBits & (CrystalBits << i)`. `reflection_serializer<2>` → ≤8 fields → 1-byte bitmap.

| Field ID | C++ field | Type | Endian |
|---|---|---|---|
| 0 | `mNounId` | u32 | **BE** |
| 1 | `mRarity` | u16 | **BE** |

Catalyst `WriteTo` (field 13 of Player reflection) emits a **16-byte fixed-size raw block** (4B NounId + 2B Rarity + 10B padding).

---

## C++ writer

`ReCap.Cpp/darkspore_server/source/RakNet/Server.cpp:1369-1409`:

```cpp
void Server::SendLabsPlayerUpdate(const ClientPtr& client, const Game::PlayerPtr& player) {
    if (!player) { return; }

    uint16_t updateBits = player->GetUpdateBits();
    if (updateBits == 0) { return; }

    BitStream outStream(8);
    outStream.Write(PacketID::LabsPlayerUpdate);

    Write<uint8_t>(outStream, player->GetId());      // BE wrapper — 1 byte slot index
    Write<uint16_t>(outStream, updateBits);           // BE wrapper

    if (updateBits & labsPlayerBits::PlayerBits) {
        player->WriteReflection(outStream);
    }
    if (updateBits & labsPlayerBits::CharacterMask) {
        for (uint32_t i = 0; i < 3; ++i) {
            if (updateBits & (labsPlayerBits::CharacterBits << i))
                player->GetCharacter(i).WriteReflection(outStream);
        }
    }
    if (updateBits & labsPlayerBits::CrystalMask) {
        for (uint32_t i = 0; i < 9; ++i) {
            if (updateBits & (labsPlayerBits::CrystalBits << i))
                player->GetCatalyst(i).WriteReflection(outStream);
        }
    }
    Send(outStream, client);
}
```

`Instance::SendLabsPlayerUpdate` (`Game/Instance.cpp:1067-1077`) wraps this: checks `player->NeedUpdate()`, broadcasts to all clients, then calls `player->ResetUpdateBits()`.

`Player::WriteReflection` is at `Game/Player.cpp:479-509`.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/LabsPlayerUpdatePacket.cs`:

```csharp
public void WriteTo(Stream stream)
{
    using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
    writer.Write(PlayerId);                          // 1 byte — no swap (matches BE wrapper, single byte)
    writer.WriteBE(UpdateBits);                      // u16 BE ✅

    if ((UpdateBits & PlayerBits) != 0 && PlayerData != null)
        PlayerData.WriteReflection(writer);          // reflection_serializer<24>

    if ((UpdateBits & CharacterMask) != 0 && PlayerData != null)
        for (int i = 0; i < 3; i++)
            if ((UpdateBits & (CharacterBits << i)) != 0 && PlayerData.Characters[i] != null)
                PlayerData.Characters[i]!.WriteReflection(writer);   // reflection_serializer<124>

    if ((UpdateBits & CrystalMask) != 0 && PlayerData != null)
        for (int i = 0; i < 9; i++)
            if ((UpdateBits & (CrystalBits << i)) != 0 && PlayerData.Catalysts[i] != null)
                PlayerData.Catalysts[i]!.WriteReflection(writer);    // reflection_serializer<2>
}
```

`LabsPlayerData.WriteReflection` at line 99, `LabsCharacterData.WriteReflection` at line 208, `LabsCatalystData.WriteReflection` at line 248.

Sent from `Game.SendLabsPlayerUpdate` (`Game.cs:141-175`) and from the periodic `Game.Update()` broadcast.

---

## Open audit items

1. **Fields 9/10/11 missing in C# `WriteReflection`.** C++ has `mCurrentCreatureId` (9), `mEnergyPoints` (10), `mbIsCharged` (11) in `Player::WriteReflection`. C# `LabsPlayerData.WriteReflection` jumps from field 8 directly to 12, omitting them. If any dataBit 9/10/11 is set (e.g. after `SwapCharacter`), the client receives no data for those fields. Verify whether C# ever sets these bits; if so, add the write.
2. **C# CrystalMask covers only indices 0–8 (9 crystals).** C++ `CrystalBottomRight = CrystalBits << 8` = bit 11. The C# `CrystalMask = 0x0FF8` covers bits 3–11, matching C++. However the C# loop goes `i < 9` (indices 0–8 → bits 3–11). This is correct but fragile — confirm the loop bound stays 9, not 8.
3. **`UpdateBits == 0` early-return in C#.** C# `SendLabsPlayerUpdate` in `Game.cs:164` returns if `updateBits == 0`. C++ does the same. Correct. No issue.
4. **PlayerId byte written raw vs BE wrapper.** C# writes `writer.Write(PlayerId)` (raw LE byte). C++ uses `Write<uint8_t>` (BE wrapper). For a single byte both produce the same result. Audit confirmed — no divergence.
5. **`ReflectionSerializer(24)` field-ID encoding.** Verify that the C# `ReflectionSerializer` with capacity=24 emits byte field-IDs + `0xFF` terminator (not a bitmap). The >16 rule from [REFLECTION_SERIALIZER.md](../REFLECTION_SERIALIZER.md) must apply. Audit the `ReflectionSerializer` implementation directly.
6. **Periodic broadcast scope.** `Instance::SendLabsPlayerUpdate` broadcasts to **all clients**. C# `Game.SendLabsPlayerUpdate` sends to **one client** (the sender). In multiplayer this will cause desyncs — other players will not receive state updates for peers.

---

## Related

- [Phase 06 Spaceship](../../flow/phases/06-spaceship.md) — initial LPU burst: 13 dataBits (bit 3 re-added 2026-05-31), updateBits=`0x17F8`
- [Phase 08 PreDungeon](../../flow/phases/08-predungeon.md) — post-SetSquad LPU: updateBits=`0x1007`, 3× character reflections
- [Phase 10 Dungeon loop](../../flow/phases/10-gameloop.md) — periodic LPU every 50 ms if any bits dirty
- [REFLECTION_SERIALIZER.md](../REFLECTION_SERIALIZER.md) — bitmap encoding rules, WriteTo vs WriteReflection
- [0x88 PlayerStatusUpdate](0x88-playerstatusupdate.md) — always triggers an LPU immediately after
- [0xAC ChainPlayerMsgs](0xAC-chainplayermsgs.md) — SetSquad triggers LPU with CharacterMask
