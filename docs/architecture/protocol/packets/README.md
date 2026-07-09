# RakNet Packet Reference

Per-opcode wire spec sheets. Single source of truth for byte-level layout. Phase docs link here instead of inlining bytes.

> **Scope:** Tier-1 (gameplay-critical) packets get individual sheets. Tier-2/3/4 (Arena, Juggernaut, KillRace, Tutorial, exotic) tracked in this index only — implement on demand.

> **Authoritative sources:**
> - C++ enum: `ReCap.Cpp/darkspore_server/source/RakNet/Types.h:24-102`
> - C# enum: `ReCap.Server/Adapters/RakNet/PacketType.cs`
> - Activator: `ReCap.Server/Adapters/RakNet/Packets/PacketActivator.cs`
> - All wire encoding follows [VERIFIED_FACTS.md](../../VERIFIED_FACTS.md) + [REFLECTION_SERIALIZER.md](../REFLECTION_SERIALIZER.md) rules. (ENDIANNESS.md deleted 2026-05-31 — superseded by VERIFIED_FACTS.md)

---

## Sheet template

Each sheet follows the same structure:

```
# 0xNN — PacketName

| Direction | Size | Phase | Status |
|---|---|---|---|

## Body layout
<offset / field / type / endian / notes>

## C++ writer / reader
file:line + 5-15 line excerpt

## C# packet class
file:line + ReadFrom / WriteTo notes

## Open audit items
```

---

## Tier-1 master table

Wire opcode → direction → size → which phase exercises it → sheet status. Click to open the per-opcode sheet.

| ID | Name | Direction | Size | Phase | Sheet |
|---|---|---|---|---|---|
| `0x7F` | [HelloPlayerRequest](0x7F-helloplayerrequest.md) | C→S | 12 B | 06 | ✅ |
| `0x80` | [HelloPlayer](0x80-helloplayer.md) | S→C | ~12 B | 06 | ⚠️ |
| `0x81` | [ReconnectPlayer](0x81-reconnectplayer.md) | S→C | 5 B | 11 | ❌ |
| `0x82` | [Connected](0x82-connected.md) | S→C | 1 B | 05 | ✅ |
| `0x83` | [Goodbye](0x83-goodbye.md) | C→S | ❓ | 13 | ❌ |
| `0x84` | [PlayerJoined](0x84-playerjoined.md) | S→C | 2 B | 05 | ✅ |
| `0x85` | [PartyMergeComplete](0x85-partymergecomplete.md) | S→C | 9 B | 06 | ✅ |
| `0x86` | [PlayerDeparted](0x86-playerdeparted.md) | S→C | 2 B | 13 | ❌ |
| `0x88` | [PlayerStatusUpdate](0x88-playerstatusupdate.md) | both | 9 B (C→S) / 2 B (S→C) | 08, 11 | ✅ |
| `0x8A` | [GameState](0x8A-gamestate.md) | S→C | 26 B | 10 | ✅ |
| `0x8B` | [DirectorState](0x8B-directorstate.md) | S→C | ~16 B | 09 | ✅ |
| `0x8C` | [ObjectCreate](0x8C-objectcreate.md) | S→C | variable | 09 | ✅ |
| `0x8D` | [ObjectUpdate](0x8D-objectupdate.md) | S→C | variable | 09 | ✅ |
| `0x8E` | [ObjectDelete](0x8E-objectdelete.md) | S→C | 5 B | 10 | ❌ |
| `0x91` | [ObjectPlayerMove](0x91-objectplayermove.md) | S→C | variable | 10 | ✅ |
| `0x94` | [LocomotionDataUpdate](0x94-locomotiondataupdate.md) | S→C | variable | 10 | ✅ |
| `0x9C` | [ActionCommandMsgs](0x9C-actioncommandmsgs.md) | C→S | variable | 10 | ✅ |
| `0xA1` | [LabsPlayerUpdate](0xA1-labsplayerupdate.md) | S→C | variable | 06, 08, 10 | ✅ |
| `0xA7` | [PlayerCharacterDeploy](0xA7-playercharacterdeploy.md) | S→C | 10 B | 09 | ✅ |
| `0xA8` | [ActionCommandResponse](0xA8-actioncommandresponse.md) | S→C | variable | 10 | ✅ |
| `0xA9` | [ChainVoteMsgs](0xA9-chainvotemsgs.md) | S→C | 2 / 339 / 6 / 3 B | 07, 11 | 🔒 |
| `0xAA` | [ChainLevelResultsMsgs](0xAA-chainlevelresultsmsgs.md) | S→C | ❓ | 11 | ❌ |
| `0xAB` | [ChainCashOutMsgs](0xAB-chaincashoutmsgs.md) | S→C | 2 / 714 B | 11 | ❌ |
| `0xAC` | [ChainPlayerMsgs](0xAC-chainplayermsgs.md) | C→S | 2 / 3 / 7 B | 07, 08 | ✅ |
| `0xAD` | [ChainGameMsgs](0xAD-chaingamemsgs.md) | S→C | 2 B | 12 | ❌ |
| `0xAE` | [ChainGameOverMsgs](0xAE-chaingameovermsgs.md) | S→C | ❓ | 12 | ❌ |
| `0xAF` | [QuickGameMsgs](0xAF-quickgamemsgs.md) | S→C | 2 B | 09 | ✅ |
| `0xB0` | [GamePrepareForStart](0xB0-gameprepareforstart.md) | S→C | 17 B | 08 | ✅ |
| `0xB1` | [GameStart](0xB1-gamestart.md) | S→C | 5 B | 09 | ✅ |
| `0xB7` | [ObjectivesInitForLevel](0xB7-objectivesinitforlevel.md) | S→C | variable | 09 | ✅ |
| `0xB8` | [ObjectiveUpdated](0xB8-objectiveupdated.md) | S→C | 24 B | 09, 10 | ✅ |
| `0xB9` | [ObjectivesComplete](0xB9-objectivescomplete.md) | S→C | variable | 11 | ✅ |
| `0xCC` | [DebugPing](0xCC-debugping.md) | both | 9 B | all | ✅ |

---

## Tier-2/3/4 inventory (not yet sheeted)

### Tier-2 — terminal/lifecycle (declared, partially used in C++ but not Tier-1)

| ID | Name | C++ status | C# status | Notes |
|---|---|---|---|---|
| `0x87` | VoteKickStarted | declared, no helper | enum + activator stub | Reserved. |
| `0x89` | GameAborted | declared, no helper | enum + activator stub | Reserved. |

### Tier-3 — gameplay loop required (out of scope until 09/10 expanded)

| ID | Name | Notes |
|---|---|---|
| `0x8F` | ObjectJump | Required for jump animations. |
| `0x90` | ObjectTeleport | Required for teleporter pads. |
| `0x92` | ForcePhysicsUpdate | Knockback / impulses. |
| `0x93` | PhysicsChanged | State change notification. |
| `0x95` | LocomotionDataUnreliableUpdate | High-frequency movement broadcast (no ack). |
| `0x96` | AttributeDataUpdate | HP / MP / buffs. |
| `0x97` | CombatantDataUpdate | Combat-relevant attribute subset. |
| `0x98` | InteractableDataUpdate | Pickup / lootable state. |
| `0x99` | AgentBlackboardUpdate | AI internal state broadcast. |
| `0x9A` | LootDataUpdate | Loot crystal data. |
| `0x9B` | ServerEvent | UI cue broadcasts (low health, overdrive ready, etc.). |
| `0x9E` | PlayerDamage | Damage taken indicator. |
| `0x9F` | LootSpawned | Loot drop visual. |
| `0xA0` | LootAcquired | Loot pickup confirmation. |
| `0xA2` | ModifierCreated | Buff/debuff applied. |
| `0xA3` | ModifierUpdated | Buff/debuff refreshed. |
| `0xA4` | ModifierDeleted | Buff/debuff expired. |
| `0xA5` | SetAnimationState | Animation state override. |
| `0xA6` | SetObjectGfxState | Visual state override. |
| `0xBA` | CombatEvent | Hit confirmation / damage roll. |
| `0xBF` | ReloadLevel | Soft level reset. |
| `0xC0` | GravityForceUpdate | Per-zone gravity override. |
| `0xC1` | CooldownUpdate | Ability cooldown sync. |
| `0xC2` | CrystalDragMessage | Pre-mission crystal grid drag. |
| `0xC3` | CrystalMessage | Crystal state notification. |
| `0xCA` | ObjectiveAdd | Add objective mid-mission. |
| `0xCB` | LootDropMessage | Drop request from client. |

### Tier-4 — alt modes / cheat / cinematic (out of scope)

| ID | Name | Notes |
|---|---|---|
| `0xB2` | (long-named cheat sentinel) | Dev-server only, never used. |
| `0xB3` | ArenaPlayerMsgs | Arena PvP. |
| `0xB4` | ArenaLobbyMsgs | Arena lobby. |
| `0xB5` | ArenaGameMsgs | Arena mid-match. |
| `0xB6` | ArenaResultsMsgs | Arena post-match. |
| `0xBB` | JuggernautPlayerMsgs | Juggernaut mode. |
| `0xBC` | JuggernautLobbyMsgs | Juggernaut lobby. |
| `0xBD` | JuggernautGameMsgs | Juggernaut mid-match. |
| `0xBE` | JuggernautResultsMsgs | Juggernaut post-match. |
| `0xC4`–`0xC7` | KillRace* | KillRace mode (lobby/game/results). |
| `0xC8` | TutorialGameMsgs | Tutorial flow. |
| `0xC9` | CinematicMsgs | Cutscene control. |

---

## Status legend

In sheets and the master table:

| Symbol | Meaning |
|---|---|
| ✅ | C# byte-for-byte matches C++ reference |
| ⚠️ | C# implemented but diverges in ≥1 field |
| ❌ | C# missing entirely or empty activator stub |
| ❓ | Body layout unverified against capture |
| 🔒 | Intentionally frozen divergence (see [CLAUDE.md](../../../../CLAUDE.md)) |

---

## How to author a new sheet

1. Identify opcode + direction.
2. Open the C++ writer in `RakNet/Server.cpp` (most live there). Note byte layout, BE/LE per field.
3. Open the C# packet class in `ReCap.Server/Adapters/RakNet/Packets/`. Diff field-by-field.
4. Copy the template at the top of this file. Fill in.
5. Mark every byte's endianness explicitly — no implicit defaults.
6. List call sites on both sides.
7. Update the master table row above (link + status).
8. Update the relevant phase doc to replace any inlined byte description with a link to this sheet.
