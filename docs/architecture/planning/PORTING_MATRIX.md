# Porting Matrix — module status (C# vs client contract, C++ as floor)

Master index of **what is ported, partial, missing, or unknown** across every module. Macro view that drives the sequenced code work and the deep-dive doc families (`components/`, `systems/`, `data-model/`, `http/`).

> **Refreshed:** 2026-07-08, via 6 parallel read-only agents (one per module) after the combat/Lua waves. Supersedes the 2026-05-25 snapshot.
>
> **★ Methodology shift (2026-07-08):** the C++ reference is **dalkon's reverse-engineered approximation** — itself incomplete and hardcoded in many places (whole components stubbed to `return true`, `#if 0`'d bodies, hardcoded ids, debug fixtures). So **"C++ parity" is a FLOOR, not the target.** The **retail client (Ghidra) is the real contract**; where the C++ reference is itself a stub, a row is flagged `⚠️C++ stub`/`⚠️C++ hardcoded` and reaching C++ parity there is *insufficient* — those need client-contract verification (Ghidra/wire), not C++ transcription. This follows the project's evidence-over-dogma rule; VERIFIED_FACTS (Ghidra) supersede stale C++ assumptions.
>
> **★ C++ path corrected:** the reference tree is `C:\CodingProjects\Personal\ReCap.Cpp\darkspore_server\source` (**with a dot** before `Cpp`). The stale `ReCapCpp` (no dot) and the even older macOS `recap_server_develop` paths were fixed 2026-07-08 across CLAUDE.md, the `cpp-ref`/`cpp-trace` commands, the `recap-cpp-tracer` agent, `settings.local.json`, and the docs (only `tools/CLAUDE.original.md` keeps the old path as an archive). During this refresh, the 3 agents dispatched before the fix (RakNet/Core/Game) could not read C++ and assessed C#-current + Ghidra facts only; SporeNet/HTTP/Blaze read the corrected path.

## Status legend

| Symbol | Meaning |
|---|---|
| ✅ | Ported — functionally equivalent C# exists |
| ⚠️ | Partial — C# exists but missing commands/fields/methods |
| ❌ | Missing — no C# counterpart |
| ❓ | Unknown — can't determine, or C#-only with no C++ match |
| N/A | Intentionally replaced by .NET BCL |
| ⚠️C++ | The **C++ reference itself** is a stub/hardcoded/disabled here — parity is not a valid target; needs client contract |

---

## Executive summary

| Module | C# coverage (2026-07-08) | Δ since 05-25 | Biggest remaining gap |
|---|---|---|---|
| [RakNet](#raknet) | opcodes 100%; inbound handlers **~85%**; outbound Send* **~55%** | ⬆ from 70%/40% | loot/modifier/cooldown/combat-event/cashout Send* family |
| [Blaze](#blaze) | **~35-40%** of C++-wired handler surface | ⬇ (old "65%" over-counted declared vs wired) | GameManager lifecycle, Util config/settings, Playgroups; CensusData absent (but C++ is an empty shell) |
| [Game](#game) | **~45%** | ⬆ from ~25% | **AI/AgentBlackboard (ObjectManager.Update is a no-op)**, NounDatabase typed assets, OctTree/Collision |
| [SporeNet](#sporenet) | ~80-85% data-model parity | ⬆ (Part.CreatureId, Room domain, rebuilt Deck) | unified `User` session object; Vendor (C++ stub); Feed persistence |
| [HTTP](#http) | ~14/22 `/game/api` methods | ⬆ (deck.updateDecks, offer-list, game:// webview) | `/qos/*`, `/game/service/png`, launcher status, account.unlock |
| [Core/QoS/Network](#core) | utils mostly BCL; FNV+logging now ✅ | ⬆ 2 rows | `QoS::Server`, general `Scheduler` |

**Headline:** The login/lobby/handshake path and now the **core combat loop** work — damage, ability casting, movement (teleport + smooth 0x95), death (0x8E), FX (0x9B), level population, real decks are all live and client-verified. The dominant gap **shifted**: it is no longer "nothing past hero-stands-in-dungeon" but **enemy behaviour** — enemies spawn but never act (`ObjectManager.Update()` is a literal no-op) — plus typed NounDatabase, spatial/collision, and the reward layer (loot/cashout).

### Cross-module blockers (re-ranked 2026-07-08, highest leverage)

1. **AI + AgentBlackboard** (Game) — `ObjectManager.Update()` is a documented no-op; enemies spawn (client-verified) but have no aggro/gambit/decided-movement. The single biggest gap for a playable dungeon.
2. **NounDatabase typed assets** (Game) — blocks AI consumption (`GameObject.AIDefinition` is read but never used), full 116-attribute fidelity, and per-noun move-speed tuning (placeholder constant today).
3. **OctTree / spatial query + Collision** (Game) — `QueryObjectsInRadius` is O(n); no collision → `AttachTriggerVolume` refs never fire, no generic trigger volumes.
4. **Reward loop: Loot + CrystalDrag + CashOut** (Game/RakNet) — `LootData`/`CashOutData` absent, ChainCashOut arm missing; the end of the dungeon loop.
5. **GameManager real game-lifecycle + matchmaking** (Blaze/Game) — but C++'s own `CreateGame` hardcodes `gameId=1` with `NotifyGameSetup` commented out; **needs client contract (Ghidra), not a C++ port.**
6. **Unified `User` session object** (SporeNet) — account/creatures/auth-token/room/game scattered across `AccountModel`+`Client`; no single `mId`/`mState`.
7. **QoS::Server + `/qos/*`** (Core/HTTP) — NAT/firewall probing absent; may block matchmaking.

---

## RakNet

UDP gameplay transport + dispatch. Opcode enum (`Adapters/RakNet/PacketType.cs`) mirrors client wire values 0x7F–0xCC. Since 05-25: action-command slots 6–13 fully wired, death path (0x8E), ability cast wiring (registry lookup + invoke), smooth-move (0x95). *(C++ tree not read this pass — stale path; C++ cites carried from prior snapshot.)*

| C++ handler | C++ file:line | C# dispatch | Status | Notes |
|---|---|---|---|---|
| `OnNewIncomingConnection` | `RakNet/Server.cpp:565` | `RakNetServer` | ✅ | sends `ConnectedPacket` (0x82) |
| `OnHelloPlayerRequest` | `RakNet/Server.cpp:596` | `Game.AttachPlayer` | ⚠️ | HelloPlayer + PartyMergeComplete; `SetCatalyst×8`/catalyst bonuses missing |
| `OnPlayerStatusUpdate` | `RakNet/Server.cpp:643` | `Game.HandlePlayerStatusUpdate` | ⚠️ | status=8→Dungeon ✅; `BeamOut` (20) ❌ |
| `OnActionCommandMsgs` | `RakNet/Server.cpp:688` | `Game.HandleActionCommand` | ✅ **(was ⚠️)** | ALL types dispatched: 3/4/5 move/stop/swap, 6 overdrive (ack), 7/8 UseAbility (→InvokeAbility), 9 CatalystPickup, 10 Cancel, 11 Interactable, 12/13 Dance/Taunt |
| `OnChainPlayerMsgs` | `RakNet/Server.cpp:988` | `Game.HandleChainPlayerMsgs` | ✅ **(was ⚠️)** | `SetSquad` from real deck (D-014), no longer hardcoded |
| `OnCrystalDragMessage` | `RakNet/Server.cpp:1026` | `Game.HandleCrystalDrag` | ✅ **(was ❌)** | replies `CrystalMessage` type=3 reject (correct for empty grid) |
| `OnLootDropMessage` | `RakNet/Server.cpp:1093` | `Game.HandlePacket` | ⚠️ **(was ❌)** | dispatched + logged; no loot logic (⚠️C++ side also an unmapped stub) |
| `OnDebugPing` | `RakNet/Server.cpp:1136` | `Game.HandleDebugPing` | ⚠️ | Spaceship/ChainVoting/Dungeon ✅; `ChainCashOut` arm ❌ |
| `PrepareGameStart` | `RakNet/Server.cpp:1210` | inlined in `HandleChainPlayerMsgs` | ✅ **(was ⚠️)** | real `SetSquad` wired |
| `RemoveClient` | `RakNet/Server.cpp:516` | `RakNetServer.OnSessionDisconnected` | ✅ | dict removal; no Game detach |
| `ObjectDelete` (0x8E, outbound) | *(server-authored)* | `Game.OnObjectDeath` | ✅ **(NEW)** | D-025 death path; Ghidra-verified flat u32 id array |

**Outbound Send* now real:** ObjectDelete (0x8E), ServerEvent (0x9B), ObjectTeleport (0x90), Locomotion{,Unreliable}Update (0x94/0x95), SetAnimationState (0xA5), ActionCommandResponse (0xA8), InteractableDataUpdate (0x98), CombatantDataUpdate (0x97), AttributeDataUpdate (0x96), CrystalMessage (0xC3), PlayerCharacterDeploy (0xA7), ObjectCreate/Update.
**Still unsent:** Modifier{Created,Updated,Deleted} (0xA2-A4), CooldownUpdate (0xC1), CombatEvent (0xBA), AgentBlackboardUpdate (0x99), Loot* (0x9A,0x9D-9F), ForcePhysics/PhysicsChanged (0x92/0x93), ObjectJump (0x8F), ObjectGfxState (0xA6), ChainCashOut/ReconnectPlayer, ObjectiveAdd (0xCA).

---

## Blaze

EA login/lobby over TCP. All 9 active components present; infra (TDF, Packet, SSL/RC4-TLS, BlazeServer) fully ported and cleaner than C++. **Coverage restated to ~35-40%** of C++-*wired* handlers — the old "65%" conflated declared `PacketID` enum sizes with actually-wired cases (traced per `ParsePacket` switch this pass).

| C++ component | C# counterpart | Status | C#/C++ wired | Key gaps |
|---|---|---|---|---|
| `AuthComponent` | `AuthenticationComponent` | ✅ | 10+3 / 10 | covers all wired + 3 extras. ⚠️C++ only wires 10 of 44 declared (persona/entitlements/console-linking unhandled in C++ too) |
| `GameManagerComponent` | `GameManagerComponent` | ⚠️ | 3 / 10 | missing CreateGame/DestroyGame/JoinGame/RemovePlayer/StartMatchmaking/etc. **⚠️⚠️C++ hardcoded**: CreateGame always returns gameId=1, NotifyGameSetup commented, JoinGame hardcodes slot=1, matchmaking is a `// TODO`. C#'s gameId=1 faithfully mirrors it → **real lifecycle needs client contract (Ghidra)** |
| `UserSessionComponent` | `UserSessionsComponent` | ✅ | 5 / 3 | covers wired + 2 extra. ⚠️C++ wires 3 of 17 declared |
| `UtilComponent` | `UtilComponent` | ⚠️ | 3 / 8 | missing `FetchClientConfig`, `GetTelemetryServer`, `SetClientMetrics`, `UserSettingsLoadAll`, `UserSettingsSave` — **real C++ handlers, genuine C# gap** |
| `MessagingComponent` | `MessagingComponent` | ⚠️ | 2 / 5 | missing SendMessage/TouchMessages/GetMessages |
| `PlaygroupsComponent` | `PlaygroupsComponent` | ⚠️ | 1 / 11 | only CreatePlaygroup; join/leave/destroy/kick inoperable; no notifications |
| `RoomsComponent` | `RoomsComponent` | ⚠️ | 3 / 4 | **CHANGED**: `JoinRoom` (0x14) now implemented (real create/join); only `SelectPseudoRoomUpdates` missing |
| `AssociationComponent` | `AssociationListsComponent` | ⚠️ | 1 / 3 | `GetLists` returns hardcoded fake entries; missing Add/RemoveUsersToList. ⚠️C++ Add/Remove are empty bodies too → client contract needed |
| `RedirectorComponent` | `RedirectorComponent` | ✅ | 1 / 1 | full |
| `CensusDataComponent` (0x0A) | — | ❌ | 0 / 3 | absent in C#. **⚠️⚠️⚠️C++ is a near-total no-op shell** (all 3 cases commented out, NotifyServerCensusData disabled) → porting "to parity" = porting nothing; build from client contract |
| infra: `Component`/`Client`/`TDF`/`Packet`/`Server`/SSL | `IComponent`/`Client`/`Tdf*`/`Packet`/`BlazeServer`/`Ssl/*` | ✅ | — | wire-identical, cleaner |
| `Functions.h` structs | `Shared.cs` (partial) | ⚠️ | — | `ReplicatedGameData`/`ReplicatedGamePlayer`/`PlaygroupInfo`/`PlayerConnectionStatus` not ported (payload for NotifyGameSetup/JoinGame) |
| — (C#-only) | `GameReportingComponent` (0x1C), `UnknownComponent1` (0x2678) | ❓ | — | no C++ match; stubs |

**Top missing:** GameManager real lifecycle (but via client contract, not C++), Util config/UserSettings, CensusData (from client contract), Playgroups near-total, `ReplicatedGameData`/`ReplicatedGamePlayer`.

---

## Game

Largest module (~21.7k LOC C++), **~45%** (up from ~25%). Combat, death, and a real Lua-driven ability engine now exist and are client-verified for the core loop.

| C++ class | C++ file:line | C# counterpart | Status | Key gaps |
|---|---|---|---|---|
| `Instance` (loop/Send*/tasks/loot) | `Game/Instance.cpp:1` | `Game` | ⚠️ **(was near-empty)** | tick, movement, cast dispatch, emotes, teleporter triggers, death broadcast real; no CashOut/DropLoot/full BeamOut/AddTask |
| `GameManager` | `Game/GameManager.cpp:1` | `GameService` | ⚠️ | no matchmaking/Criteria/Rule/RemoveGame |
| `ObjectManager` | `Game/ObjectManager.cpp:1` | `ObjectManager` | ⚠️ **(was ❌)** | Spawn/Remove/attribute-seed real; **`Update()` is a no-op stub** — no spatial query/LoS/AI/locomotion sim |
| `TriggerVolume` | `Game/ObjectManager.h:19` | teleporter triggers + `AttachTriggerVolume` (refs held, firing deferred) | ⚠️ **(was ❌)** | generic collision-based volumes absent |
| `Object`/`cGameObject` | `Game/Object.cpp:1` | `GameObject` + `SporelabsObject` | ⚠️ **(was wire-only)** | HP/Team/PlayerId/Target/Attributes/Dead/Locomotion-goal modeled; no CombatantData struct, no persistent Modifier list, no cooldowns, no AI/Blackboard |
| `CombatantData` | `Game/Object.h:60` | folded into `GameObject.Health/MaxHealth/Target` | ⚠️ **(was ❌)** | no mana pool, no sub-object |
| `InteractableData` | `Game/Object.h:77` | `_interactableTimesUsed` dict | ⚠️ **(was ❌)** | TimesUsed only; no UsesAllowed/Ability gating |
| `LootData` | `Game/Object.h:105` | — | ❌ | loot phase not started |
| `AgentBlackboard` | `Game/Object.h:155` | — | ❌ | absent |
| `EffectList` | `Game/Object.h:36` | `EmitEffect` (fire-and-forget 0x9B) | ⚠️ **(was ❌)** | no active-effect list/duration |
| `Modifier` | `Game/Object.h:190` | `AddAttributeModifier` (additive delta) | ⚠️ **(was ❌)** | no reversal/expiry; ⚠️ additive-vs-mult unverified (DEFERRED in code) |
| `AI` (inner) | `Game/Object.h:217` | — | ❌ | **no aggro/gambit — `ObjectManager.Update()` is a no-op. #1 gap.** |
| `Player` | `Game/Player.cpp:1` | `Player` + `LabsPlayerData` | ⚠️ | real SetSquad + SwapCharacter + ability-slot resolution; no XP/level, overdrive stub, no UpdateCatalystBonuses |
| `Character` | `Game/Character.cpp:1` | `LabsCharacterData` | ⚠️ | no full `mPartAttributes` from assets (D-001); live HP not mirrored back |
| `Catalyst` | `Game/Catalyst.cpp:1` | `LabsCatalystData` | ⚠️ | wire only, hardcoded catalysts |
| `Attributes` (114-attr) | `Game/Attributes.cpp:1` | `GameObject.Attributes` + natives | ⚠️ **(was ❌)** | **damage/heal math works** (client-verified Pummel roll); only ids 0/1/2/4/101/102 populated of 116 |
| `Locomotion` | `Game/Locomotion.cpp:1` | `LocomotionData` + goal fields + `NLocomotionModule` | ⚠️ **(was wire-only)** | teleport+smooth (0x95) client-verified; no projectile/orbit/roll/jump sim, no collision, MoveSpeed placeholder |
| `Lua`/`LuaThread`/`Coroutine` | `Game/Lua.cpp`, `LuaFunctions.cpp` | `Adapters/Scripting/` + `Services/Scripting/` | ⚠️ **(biggest change)** | **P0-P4 landed:** native Lua 5.1.4, VFS, 28 namespaces (18 with real natives, ~27 native fns across waves), predicate scheduler, cast wiring live end-to-end, `_luaGate` thread-safe. 10 namespaces still bare stubs (nBehaviorTree, nScenarioManager, nPhysics, nAgent, nGameDirector, nLevel, nJuggernaut, nTuning, nClient, + nCondition/nAffix registrar-only) |
| `Ability` | `Game/Lua.h:293` | `NAbilityContextModule` + `InvokeAbility` | ⚠️ | register + 7-arg tick dispatch + combat natives all real; no mana/cooldown enforcement (Pay* is a no-op stamp), no Activate/Deactivate |
| `Objective` | `Game/Lua.h:366` | `nObjective` stub | ⚠️ | 13 registered at boot; no runtime coroutine |
| `OctTree` | `Game/Octree.cpp:1` | — | ❌ | no spatial index; `QueryObjectsInRadius` is O(n) linear |
| `Noun`/`NounDatabase` | `Game/Noun.cpp:1` | raw `AssetValue` navigation | ❌ | no typed Noun/NPC/PlayerClass/AIDefinition/ClassAttributes; **AIDefinition read but never consumed** |
| `Level`/`Markerset` | `Game/Level.cpp:1` | `Chain.PopulateFromLevel` + `Game.PopulateLevel` | ⚠️ **(improved)** | structured 3-pass loader (D-011); no typed Level/LevelConfig/difficulty |
| `ServerEvent`/`CombatEvent` | `Game/ServerEvent.h:85` | `ServerEventPacket` + `BroadcastServerEvent`/`EmitEffect` | ⚠️ **(was ❌)** | 0x9B client-verified (26-field); no CombatEvent sub-types, no damage-number/combat-log |
| `CashOutData` | `Game/Instance.h:86` | — | ❌ | ChainCashOut unimplemented |
| `GameInfo`/`Rule`/`Criteria`/`Matchmaking` | `Game/Instance.h:30` | partial | ❌ | no matchmaking structs |
| `Party`/`PartyManager` | `Game/Party.h:21` | — | ❌ | |
| `Collision`/Bounding* | `Game/Collision.cpp:1` | — | ❌ | no server collision → trigger firing deferred |
| `AssetData::*` (DBPF) | `Game/AssetData/*` | `AssetDatabase` | ✅ | full |
| `GameObjectCreateData` | (Object::WriteTo) | `GameObjectCreateData` | ✅ | full |

**Top by importance (re-ordered):** (1) AI+AgentBlackboard, (2) NounDatabase typed assets, (3) OctTree/spatial+Collision, (4) remaining Lua stub namespaces + persistent Modifier/EffectList, (5) Loot/CashOut, (6) ObjectManager lifecycle refinements (LoS, MarkForDeletion sweep), (7) Locomotion sim depth (projectile/jump/collision), (8) Level/LevelConfig typed model.

---

## SporeNet

User/account/creature data model. **~80-85%** field parity. Since 05-25: `Part.CreatureId` present, Room/RoomManager domain added, Deck rebuilt (player-driven, ownership-validated).

| C++ class | C# counterpart | Status | Notes |
|---|---|---|---|
| `Account`/`Stats`/`AbilityStat`/`TemplateDatabase`/`Parts`/`PartRarity`/`Squads`/`Creatures` | domain + models + services | ✅ | field parity |
| `Part` | `CreaturePart`/`CreaturePartModel` | ✅ **(CHANGED)** | `CreatureId` (mEquippedToCreatureId) now present + persisted |
| `Squad` | `Deck`/`DeckModel` | ✅ **(CHANGED)** | player-driven, ownership-validated `updateDeck`; deliberately skips C++'s `// TODO: remove` slot-0 auto-fill hack |
| `Room`/`RoomView`/`RoomCategory`/`RoomManager` | `RoomManager.cs` | ⚠️ **(was ❌)** | domain + membership + `joinRoom` wired; in-memory only, 4 hardcoded rooms; no leave/destroy. ⚠️C++ `CreateRoomView` has live TODO |
| `User` (live session) | fragmented `AccountModel`+`Client` | ⚠️ | no unified object; `mId`/`mState`/`mRoom`/`mGame` scattered; auth token in-memory on Client |
| `UserManager` | `AccountService` | ⚠️ | no GetAllUsers, no assoc-by-token lookup |
| `TemplateCreature`/`Creature` | models | ⚠️ | element/class as strings; no `mCreatorId`/GearScoreFlattened persisted. ⚠️C++ creature-id gen is `// TODO` per-account sequential (C# EF identity is better) |
| `Vendor` | — | ❌ | **⚠️⚠️C++ near-total stub**: OnBuy no-op, OnSell never refunds, offer-list hardcodes 50 parts w/ magic stats → **client contract needed**, don't port C++ |
| `Feed`/`FeedItem` | XML DTOs | ⚠️ | not persisted; ⚠️C++ `WriteCreaturesAPI` is empty TODO |
| `AssociationLists` | `AssociationListsComponent` | ⚠️ | hardcoded fake friends, no persistence. ⚠️C++ Add/Remove are empty bodies → client contract needed |
| `CreatureParts`/`CreatureType`/`CreatureClass` | bools/strings | ⚠️/❌ | no typed enums; ⚠️C++ classType→parts mapping is self-flagged provisional |

**Top gaps (real, not C++-floor):** unified `User` session object; Room persistence + leave/destroy; Feed persistence.
**Do NOT chase to C++ parity (C++ itself stub):** Vendor economy, AssociationLists add/remove, leaderboard — need Ghidra/wire.

---

## HTTP

REST API. **~14/22** `/game/api` methods functional. Since 05-25: `deck.updateDecks` persists, `getPartOfferList` returns valid-empty, new `game://` in-game webview serving (`GameStorageAdapter`), launcher templating expanded.

| Endpoint | C# handler | Status | Notes |
|---|---|---|---|
| `api.deck.updateDecks` | `GameRestController.updateDecks` | ✅ **(was ❌)** | real persistence via `DeckService`, ownership-validated |
| `api.inventory.getPartOfferList` | `getPartOfferList` | ⚠️ **(was ❌)** | valid-empty XML; ⚠️C++ Vendor is stub |
| `api.inventory.vendorParts` | `getVendorParts` | ⚠️ | sell/flair done (**more complete than C++**); weapon/parts TODO both sides |
| `api.account.setSettings` | `setPlayerAccountSettings` | ⚠️ | **C# persists; ⚠️C++ never persists (empty loop)** |
| `game://` webview (`GameStorageAdapter`) | new | ✅ **(NEW)** | serves Web.package DBPF + HTML shim; no C++ equiv (client-contract reimpl) |
| `/bootstrap/launcher/*` templating | `StaticStorageAdapter` | ⚠️ **(changed)** | templates host/version/mode/isDev; missing version-lock vars |
| account.auth/getAccount/logout, creature.getCreature/unlock/updateCreature, status.*, inventory.getPartList/updatePartStatus, config.getConfigs, recap.registration/log | controllers | ✅ | parity |
| `api.account.unlock` | `unlockPlayerAccount` | ❌ | C++ real (`User::UnlockUpgrade`); C# null-stub |
| `api.creature.getTemplate` | `getTemplate` | ❌ | C++ real; C# `// TODO` null |
| `api.account.searchAccounts` | `searchPlayerAccounts` | ❌ | C# null; ⚠️C++ sort branches empty |
| `api.game.{getGame,getRandomGame,exitGame}` | stubs | ⚠️ | ⚠️C++ hardcoded fixture data → client contract |
| `api.leaderboard.getLeaderboard` | — | ❌ | ⚠️C++ stub (echoes category names only) — low priority |
| `/game/service/png`, `/template_png/*`, `/creature_png/*` | — | ❌ | dynamic thumbnail route absent; UI thumbnails 404 |
| `/qos/qos`, `/qos/firewall`, `/qos/firetype` | — | ❌ | NAT/firewall XML absent (C++ mostly real); may block matchmaking |
| `/recap/api?method=api.game.status` | — | ❌ | launcher progress/play-button broken |

**Top gaps:** `/qos/*`, `/game/service/png`, `api.game.status`, `api.account.unlock`, `api.creature.getTemplate`, `api.game.getGame` (via client contract).

---

## Core / QoS / Network

Low-level utilities + transport; most C++ utils intentionally BCL-replaced. Since 05-25: FNV-1a exposed, logging modernized.

| C++ class | C# counterpart | Status | Notes |
|---|---|---|---|
| `utils::hash_id` (FNV-1a) | `WireHash.Fnv1a` via `ScriptVfs.Hash` | ✅ **(was ❌)** | exposed + used (anim id hashing, asset lookup) |
| `utils::extended`/log | `Util/Logging/*` (Serilog facade) | ✅ **(was ⚠️)** | ANSI theme + timestamps + per-category switches; old `Util/Logger.cs` deleted |
| `Scheduler` (AddTask/CancelTask) | `LuaCoroutineScheduler` (Lua-scoped) + 50ms `Task.Delay` loop | ❌ | no general cancellable Core primitive; Lua scheduler doesn't cover non-Lua tasks |
| `QoS::Server` | — (only Blaze `QosConfigInfo` static ping-site stub) | ❌ | no UDP/REST QoS responder; `/qos/*` absent |
| `DataBuffer` | `BigEndianExtensions` | ⚠️ | BE/LE covered |
| `utils::random` | scattered `Random.Shared` | ❌ | no typed helper |
| `utils::net::resolve_ip` | — | ❌ | no DNS wrapper |
| `utils::xml_*node` | `Util/XmlHelper.cs` | ⚠️ | serialize-only |
| `Network::Client` (abstract) | Blaze/RakNet clients | ⚠️ | no shared base |
| `Version.h` | `ServerConfigOptions` | ⚠️ | game version only, not server self-version |
| `Task`/`json`/`enum`/string-time utils | BCL | N/A | |

**Top gaps:** `QoS::Server`, general `Scheduler`, `utils::random` typed helper, `resolve_ip`, `xml_*node` typed overloads.

---

## How to keep this in sync

- **Snapshot** (refreshed 2026-07-08). When a row's status changes, edit the row + the executive summary in the same turn.
- Deep-dive docs (`components/`, `data-model/`, `http/`, `systems/`) expand individual rows.
- Re-run the 6-agent fan-out after major milestones. **Use the correct C++ path `C:\CodingProjects\Personal\ReCap.Cpp`** and keep the C++-is-a-floor / client-is-the-contract framing.
