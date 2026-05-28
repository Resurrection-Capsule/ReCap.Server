# Porting Matrix — C++ ↔ C# class/handler parity

Master index of **what is ported, partial, missing, or unknown** across every C++ module versus the ReCap C# server. This is the macro view that drives [`PORTING_PLAN.md`](PORTING_PLAN.md) (sequenced code work) and the deep-dive doc families (`components/`, `systems/`, `data-model/`, `http/`).

> **Generated:** 2026-05-25, via 6 parallel read-only agents (one per C++ module). Sonnet-class pass — class/handler inventory + status, not byte-level. Deep-dives per subsystem land separately.
>
> **C++ root:** `C:\CodingProjects\Personal\ReCapCpp\darkspore_server\source`. C++ cites are relative to it (e.g. `Game/Instance.cpp:452`). C# cites relative to `ReCap.Server/`.

## Status legend

| Symbol | Meaning |
|---|---|
| ✅ | Ported — functionally equivalent C# class/handler exists |
| ⚠️ | Partial — C# exists but missing commands/fields/methods |
| ❌ | Missing — no C# counterpart |
| ❓ | Unknown — can't determine, or C#-only with no C++ match |
| N/A | Intentionally replaced by .NET BCL (no port needed) |

---

## Executive summary

| Module | C++ LOC | C# coverage | Biggest gap |
|---|---|---|---|
| [RakNet](#raknet) | 5.2k | opcodes 100%; handlers ~70%; Send* ~40% | ~25 outbound `Send*` (loot/modifier/combat/ability/reconnect/cashout) missing |
| [Blaze](#blaze) | 8.9k | ~65% handler surface | GameManager lifecycle (CreateGame/JoinGame/matchmaking), CensusData, Playgroups |
| [Game](#game) | 21.7k | **~25%** | Entire combat engine: ObjectManager, Attributes, Lua/abilities, AI, NounDatabase, Locomotion sim |
| [SporeNet](#sporenet) | 3.3k | ~80% data model | Unified `User` session object, Room/Vendor/Feed, AssociationLists persistence |
| [HTTP](#http) | 1.1k | path-level full; ~7/20 game-api stubs `null` | `/qos/*`, `/game/service/png`, deck/game/leaderboard stubs |
| [Core/QoS/Network](#core) | 1.9k | utils mostly via BCL | `QoS::Server`, `Scheduler` (AddTask/CancelTask) |

**Headline:** Login/lobby/handshake path is largely ported. The **Game module (combat engine) is the dominant gap** — ~75% unimplemented, and it's the largest module. Everything past "hero stands in dungeon" (damage, abilities, AI, loot, objectives, cashout) is absent.

### Cross-module blockers (highest leverage)

1. **Game/ObjectManager + Object lifecycle** — no server-side entity create/update/delete/death pipeline. Blocks all dungeon state. (Game)
2. **Attributes + combat math** (`TakeDamage`/`Heal`/crit/damage distribution) — no damage possible. Blocks abilities + AI. (Game)
3. **Lua VM + Ability/Objective system** — all gameplay scripting absent (~3.2k LOC in `LuaFunctions.cpp` alone). (Game)
4. **NounDatabase typed asset loading** — creature/NPC stats + AI defs not typed; spawning uses hardcoded noun IDs. (Game)
5. **`SetSquad` from real user data** — PrepareGameStart ignores the account loadout, hardcodes one creature for all 3 slots. (RakNet/Game/SporeNet)
6. **Unified `User` session object** — C# scatters account/creatures/squads/auth-token/room/game across `AccountModel`+`Client`+`GameService`; no single authoritative session entity, no `mId`/`mState`. (SporeNet)
7. **QoS::Server + `/qos/*` endpoints** — NAT/firewall probing absent on UDP and HTTP; may block matchmaking/login. (Core/HTTP)

---

## RakNet

UDP gameplay transport + packet dispatch. Opcode enum (`PacketType.cs`) mirrors C++ `Types.h::PacketID` exactly (all 49 values, 0x7F–0xCC). Inbound gameplay handlers mostly wired; outbound `Send*` universe severely incomplete.

| C++ handler | C++ file:line | C# dispatch site | Status | Notes |
|---|---|---|---|---|
| `OnNewIncomingConnection` | `RakNet/Server.cpp:565` | `RakNetServer.OnSessionOnNewIncomingConnection` | ✅ | Sends `ConnectedPacket` (0x82) |
| `OnHelloPlayerRequest` | `RakNet/Server.cpp:596` | `Game.AttachPlayer` | ⚠️ | HelloPlayer + PartyMergeComplete done; `SetCatalyst×8` + catalyst bonuses missing; no `mId` |
| `OnPlayerStatusUpdate` | `RakNet/Server.cpp:643` | `Game.HandlePlayerStatusUpdate` | ⚠️ | status=8→Dungeon ✅; `BeamOut` (status=20) ❌ |
| `OnActionCommandMsgs` | `RakNet/Server.cpp:688` | `Game.HandleActionCommand` | ⚠️ | Move/Stop/Swap (cmd 3/4/5) ✅; UseAbility (7/8), CatalystPickup (9), Cancel (10), Interactable (11), Dance/Taunt (12/13) ❌ |
| `OnChainPlayerMsgs` | `RakNet/Server.cpp:988` | `Game.HandleChainPlayerMsgs` | ⚠️ | vote init + PreDungeon + StayInParty ✅; `SetSquad` from real data ❌ (hardcoded) |
| `OnCrystalDragMessage` | `RakNet/Server.cpp:1026` | — | ❌ | empty `PacketActivator` stub; no logic |
| `OnLootDropMessage` | `RakNet/Server.cpp:1093` | — | ❌ | empty `PacketActivator` stub; no logic |
| `OnDebugPing` | `RakNet/Server.cpp:1136` | `Game.HandleDebugPing` | ⚠️ | Spaceship/ChainVoting/Dungeon arms ✅; ChainCashOut arm ❌; `SwapCharacter` post-OnPlayerStart ❌ |
| `PrepareGameStart` | `RakNet/Server.cpp:1210` | inlined in `HandleChainPlayerMsgs` | ⚠️ | GamePrepareForStart + state→PreDungeon ✅; `SetSquad` real data ❌ |
| `RemoveClient` | `RakNet/Server.cpp:516` | `RakNetServer.OnSessionDisconnected` | ✅ | dict removal only; no Game detach |

**Top missing:** `OnCrystalDragMessage`, `OnLootDropMessage`, ChainCashOut DebugPing arm, `SwapCharacter(player,1)` post-deploy, `BeamOut`, ability command arms, real `SetSquad`. **~25 outbound `Send*` absent:** ObjectDelete, Modifier{Created,Updated,Deleted}, CooldownUpdate, CombatEvent, ServerEvent, AttributeDataUpdate, CombatantDataUpdate, InteractableDataUpdate, AgentBlackboardUpdate, LootDataUpdate, ActionCommandMessages, ChainCashOutMessages, ReconnectPlayer, ObjectJump, ObjectTeleport, ForcePhysicsUpdate, PhysicsChanged, LocomotionDataUnreliableUpdate, AnimationState, ObjectGfxState, CrystalMessage, ObjectiveAdd/Update.

---

## Blaze

EA login/lobby over TCP. All 9 active components present; handler depth ranges from full (Redirector, Auth) to stub-only (Playgroups, Rooms). Infrastructure (TDF codec, Packet, SSL/RC4-TLS, BlazeServer) fully ported and cleaner than C++.

| C++ class | C++ file:line | C# counterpart | Status | Key gaps |
|---|---|---|---|---|
| `AuthComponent` | `Blaze/Component/AuthComponent.cpp:309` | `AuthenticationComponent` | ⚠️ | C++ 10 handlers (incl. `GetAuthToken`, `SilentLogin`, `LoginPersona`); C# adds 3 TOS cmds |
| `GameManagerComponent` | `Blaze/Component/GameManagerComponent.cpp:580` | `GameManagerComponent` | ⚠️ | C# handles 3/11; missing `CreateGame`, `DestroyGame`, `JoinGame`, `RemovePlayer`, `StartMatchmaking`, `CancelMatchmaking` |
| `UserSessionComponent` | `Blaze/Component/UserSessionComponent.cpp:75` | `UserSessionsComponent` | ⚠️ | extra C# handlers are stubs; no game-critical gap |
| `UtilComponent` | `Blaze/Component/UtilComponent.cpp:82` | `UtilComponent` | ⚠️ | C# 3/9; missing `FetchClientConfig`, `GetTelemetryServer`, `UserSettingsSave`, `UserSettingsLoadAll` |
| `MessagingComponent` | `Blaze/Component/MessagingComponent.cpp:81` | `MessagingComponent` | ⚠️ | C# 2/5; missing `SendMessage`, `TouchMessages`, `GetMessages` |
| `PlaygroupsComponent` | `Blaze/Component/PlaygroupsComponent.cpp:85` | `PlaygroupsComponent` | ⚠️ | C# 1/12 + no notifications; join/leave/destroy inoperable |
| `RoomsComponent` | `Blaze/Component/RoomsComponent.cpp:187` | `RoomsComponent` | ⚠️ | C# 2/4; missing `JoinRoom`, all Notify* |
| `AssociationComponent` | `Blaze/Component/AssociationComponent.cpp:134` | `AssociationListsComponent` | ⚠️ | C# 1/3; missing `AddUsersToList`, `RemoveUsersFromList` |
| `RedirectorComponent` | `Blaze/Component/RedirectorComponent.cpp:123` | `RedirectorComponent` | ✅ | full |
| `CensusDataComponent` | `Blaze/Component/CensusDataComponent.cpp:47` | — | ❌ | component 0x0A entirely absent (3 commands) |
| `Component` (base) | `Blaze/Component.cpp:14` | `IComponent` | ✅ | interface + dict dispatch, cleaner |
| `Client` | `Blaze/Client.h:20` | `Client` | ✅ | session/user binding |
| `TDF::Packet`/`Parser` | `Blaze/TDF.h:94` | `TdfEncoder`/`TdfDecoder` | ✅ | split encode/decode; +TdfMap/TdfVector |
| `Blaze::Packet` | `Blaze/Packet.h:24` | `Packet` | ✅ | wire-identical |
| `Blaze::Server` | `Blaze/Server.h:13` | `BlazeServer` | ✅ | hosts redirector(TLS)+lobby from one class |
| `Functions.h` structs | `Blaze/Functions.h:11` | `Shared.cs` (partial) | ⚠️ | `ReplicatedGameData`, `ReplicatedGamePlayer`, `PlaygroupInfo`, `PlayerConnectionStatus` not ported |
| SSL layer | `Blaze/Server.h:29` | `Ssl/*.cs` | ✅ | explicit RC4-TLS 1.0 via BouncyCastle |
| — (C#-only) | — | `GameReportingComponent` (0x1C) | ❓ | no C++ match, stub only |
| — (C#-only) | — | `UnknownComponent1` (0x2678) | ❓ | handles unidentified cmd 0x200 |

**Top missing:** GameManager game-lifecycle cmds, UtilComponent `FetchClientConfig`/UserSettings, CensusData component, Playgroups near-total, `ReplicatedGameData`/`ReplicatedGamePlayer` structs (payload for `NotifyGameSetup`/`JoinGame`).

---

## Game

Largest module (~21.7k LOC) and the dominant gap. C# implements only the happy-path network flow (connect → vote → predungeon → dungeon entry). The entire combat engine is absent.

| C++ class | C++ file:line | C# counterpart | Status | Key gaps |
|---|---|---|---|---|
| `Instance` (game loop, Send*, tasks, loot) | `Game/Instance.cpp:1` | `Game` (`Domain/Gameplay/Game.cs`) | ⚠️ | no AddTask/MoveObject/UseAbility/SwapCharacter/DropLoot/BeamOut/InteractWith/Send{ServerEvent,CombatEvent,Cooldown,Gfx,Anim}/LoadLevel |
| `GameManager` | `Game/GameManager.cpp:1` | `GameService` | ⚠️ | no matchmaking/Criteria/Rule/RemoveGame |
| `ObjectManager` | `Game/ObjectManager.cpp:1` | — | ❌ | no object lifecycle, spatial query, LoS, MarkForDeletion |
| `TriggerVolume` | `Game/ObjectManager.h:19` | — | ❌ | absent |
| `Object`/`cGameObject` | `Game/Object.cpp:1` | `SporelabsObject` | ⚠️ | only wire-serialization; no Combatant/Interactable/Loot/Blackboard/Effect/Modifier/AI, no TakeDamage/Heal/OnDeath, no cooldowns/physics |
| `CombatantData` | `Game/Object.h:60` | — | ❌ | |
| `InteractableData` | `Game/Object.h:77` | — | ❌ | |
| `LootData` | `Game/Object.h:105` | — | ❌ | |
| `AgentBlackboard` | `Game/Object.h:155` | — | ❌ | |
| `EffectList` | `Game/Object.h:36` | — | ❌ | |
| `Modifier` | `Game/Object.h:190` | — | ❌ | |
| `AI` (inner) | `Game/Object.h:217` | — | ❌ | aggro, gambit execution |
| `Player` | `Game/Player.cpp:1` | `Player` + `LabsPlayerData` | ⚠️ | no SetSquad (hardcoded nouns), SwapCharacter, ability rank lookup, XP/level, overdrive, UpdateCatalystBonuses |
| `Character` | `Game/Character.cpp:1` | `LabsCharacterData` | ⚠️ | no mPartAttributes from assets, ranks hardcoded, no live HP, no ResetUpdateBits |
| `Catalyst` | `Game/Catalyst.cpp:1` | `LabsCatalystData` | ⚠️ | NounId+Rarity + wire only; no Type/Color enums, no construction from LootData |
| `Attributes` (114-attr array) | `Game/Attributes.cpp:1` | — | ❌ | no attribute system → no damage/heal math |
| `Locomotion` | `Game/Locomotion.cpp:1` | `LocomotionData` | ⚠️ | wire fields only; no Update sim (projectile/orbit/roll/jump), no SetGoalObject, no collision |
| `LobParameters` | `Game/Locomotion.h:73` | `LobParams` | ⚠️ | wire only |
| `ProjectileParameters` | `Game/Locomotion.h:95` | `ProjectileParams` | ⚠️ | wire only |
| `Lua`/`GlobalLua`/`LuaThread`/`Coroutine` | `Game/Lua.cpp:1`, `Game/LuaFunctions.cpp:1` | — | ❌ | entire scripting VM + ability engine absent |
| `Ability` | `Game/Lua.h:293` | — | ❌ | no Activate/Deactivate/Tick/mana/range |
| `Objective` | `Game/Lua.h:366` | — | ❌ | wire stub exists; coroutine not impl |
| `OctTree` | `Game/Octree.cpp:1` | — | ❌ | spatial acceleration |
| `Noun`/`NounDatabase` | `Game/Noun.cpp:1` | — | ❌ | no typed Noun/NPC/PlayerClass/AIDefinition/ClassAttributes/Animation/Phase |
| `Level`/`Markerset`/`Marker` | `Game/Level.cpp:1` | partial via `AssetDatabase.GetLevelMarkers` | ⚠️ | no typed Level/LevelConfig/DirectorClass/Teleporter/difficulty |
| `ServerEvent`/`ClientEvent`/`CombatEvent` | `Game/ServerEvent.h:85` | — | ❌ | no event serialization (DirectorState stub only) |
| `CashOutData` | `Game/Instance.h:86` | — | ❌ | ChainCashOut unimplemented |
| `GameInfo`/`Rule`/`Criteria`/`Matchmaking` | `Game/Instance.h:30` | partial (GameService IDs) | ❌ | no matchmaking structs |
| `Party`/`PartyManager` | `Game/Party.h:21` | — | ❌ | |
| `Config` | `Game/Config.cpp:1` | `ServerConfig` | ⚠️ | covers needed keys; enum-key model not replicated |
| `Collision`/Bounding* | `Game/Collision.cpp:1` | — | ❌ | |
| `API` (REST, 30+ endpoints) | `Game/API.cpp:1` | `Adapters/Rest/*` | ⚠️ | see [HTTP](#http) |
| `AssetData::*` (DBPF) | `Game/AssetData/*` | `AssetDatabase` (lib submodule) | ✅ | DBPF read + cache + named lookup |
| `GameObjectCreateData` | (in Object::WriteTo) | `GameObjectCreateData` | ✅ | full WriteTo + WriteReflection |

**Top 8 by porting importance:** (1) ObjectManager+Object lifecycle, (2) Attributes+combat math, (3) Lua VM+Ability/Objective, (4) NounDatabase typed assets, (5) AI+AgentBlackboard, (6) Locomotion simulation, (7) ServerEvent/CombatEvent broadcasting, (8) Level/LevelConfig/CashOutData.

---

## SporeNet

User/account/creature data model. Core entities well-ported (~80% field parity) with EF Core/SQLite persistence. Runtime session constructs and several subsystems absent.

| C++ class | C++ file:line | C# counterpart | Status | Key gaps |
|---|---|---|---|---|
| `Account` | `SporeNet/User.h:31` | `Account`/`AccountModel` | ✅ | C# adds Email/settings; id int64→ulong |
| `FeedItem` | `SporeNet/User.h:78` | `FeedItemContract` | ⚠️ | contract-only, no entity/persistence |
| `Feed` | `SporeNet/User.h:91` | `FeedContract` (XML DTO) | ⚠️ | not persisted |
| `User` (live session object) | `SporeNet/User.h:111` | fragmented `AccountModel`+`Client` | ⚠️ | no unified object; `mId`/`mState`/`mRoom`/`mGame` scattered/absent; auth token in-memory only |
| `UserManager` | `SporeNet/User.h:223` | `AccountService` | ⚠️ | no GetAllUsers, no in-memory assoc lookup |
| `Stats` | `SporeNet/Creature.h:130` | `CreatureModelStat` | ✅ | string-serialized |
| `AbilityStat` | `SporeNet/Creature.h:138` | `CreatureModelAbilityStat` | ✅ | string-serialized |
| `TemplateCreature` | `SporeNet/Creature.h:145` | `CreatureTemplateModel` | ⚠️ | element/class as strings; ability[5] split; no domain entity |
| `TemplateDatabase` | `SporeNet/Creature.h:186` | `CreatureTemplateRepositoryAdapter` | ✅ | JSON→SQLite, equivalent |
| `Creature` | `SporeNet/Creature.h:200` | `Creature`/`CreatureModel` | ⚠️ | no mCreatorId in domain; GearScoreFlattened not persisted |
| `Creatures` | `SporeNet/Creature.h:258` | `CreatureService` | ✅ | |
| `Part` | `SporeNet/Part.h:31` | `CreaturePart`/`CreaturePartModel` | ⚠️ | **missing `mEquippedToCreatureId`**; asset hashes u32→ulong |
| `Parts` | `SporeNet/Part.h:98` | `CreaturePartService` | ✅ | |
| `PartRarity` | `SporeNet/Part.h:20` | `CreaturePartRarity` | ✅ | exact |
| `PartUsage` | `SporeNet/Part.h:15` | int field only | ❌ | C++ enum empty (reserved) |
| `Squad` | `SporeNet/Squad.h:11` | `Deck`/`DeckModel` | ⚠️ | renamed Squad→Deck; pvp flag via Category string |
| `Squads` | `SporeNet/Squad.h:46` | `DeckService` | ✅ | |
| `Room` | `SporeNet/Room.h:84` | `RoomsComponent` (stub) | ❌ | no domain entity, no membership |
| `RoomView`/`RoomCategory` | `SporeNet/Room.h:27,52` | TDF DTOs | ❌ | wire-only |
| `RoomManager` | `SporeNet/Room.h:123` | — | ❌ | no lifecycle |
| `Vendor` | `SporeNet/Vendor.h:15` | — | ❌ | buyback is TODO |
| `CreatureType`/`CreatureClass` | `SporeNet/Creature.h:62,72` | string fields | ⚠️ | no typed enum |
| `CreatureParts` | `SporeNet/Creature.h:80` | — | ❌ | approximated by hasHands/hasFeet bools |
| `SporeNet::Instance` | `SporeNet/Instance.h:12` | DI container | ⚠️ | service-locator → ASP.NET DI |
| `AssociationLists` | `SporeNet/User.h:199` | `AssociationListsComponent` (stub) | ⚠️ | hardcoded empty, no persistence |

**Top 5:** `Part.mEquippedToCreatureId` missing; Room/RoomManager absent as domain; Vendor/buyback unimplemented; Feed not persisted; unified `User` session object missing (no `mId`/`mState`).

---

## HTTP

REST API (launcher, bootstrap, asset serving). All 5 C++ route groups present at path level; ~7/20 `/game/api` methods return `null`; three route families (`/qos/*`, `/web/sporelabs*`, static launcher templating) have no dedicated C# handler.

Selected divergences (full table in agent run; key rows):

| Endpoint | C++ file:line | C# handler | Status | Notes |
|---|---|---|---|---|
| `/api`, `/telemetryevent` | `Game/API.cpp:319,325` | — | ❌ | trivial stubs absent |
| `/recap/api?method=api.game.status` | `Game/API.cpp:833` | — | ❌ | launcher progress/play-button broken |
| `/recap/api?method=api.panel.*` | `Game/API.cpp:848+` | — | ❌ | commented out in C++ too |
| `/bootstrap/launcher/*` | `Game/API.cpp:374,393` | StaticStorage fallthrough | ⚠️ | C++ templates `{{host}}`/`{{version}}`; C# serves raw |
| `/game/api?method=api.inventory.getPartOfferList` | `Game/API.cpp:1068` | `GameRestController` | ⚠️ | empty list; vendor TODO |
| `/game/api?method=api.account.unlock` | `Game/API.cpp:1517` | `GameRestController` | ⚠️ | returns null |
| `/game/api?method=api.game.{getGame,getRandomGame,exitGame}` | `Game/API.cpp:1615+` | `GameRestController` | ⚠️ | null stubs |
| `/game/api?method=api.deck.updateDecks` | `Game/API.cpp:1866` | `GameRestController` | ⚠️ | null — deck saves never persist |
| `/game/api?method=api.leaderboard.getLeaderboard` | `Game/API.cpp:1883` | `GameRestController` | ⚠️ | null |
| `/game/service/png` | `Game/API.cpp:541` | — | ❌ | template PNG by id+size; UI thumbnails 404 |
| `/qos/qos`, `/qos/firewall`, `/qos/firetype` | `Game/API.cpp:591,633,681` | — | ❌ | NAT/firewall probe XML absent |

**Top 5:** `/qos/*` family, `/game/service/png`, `api.game.status`, `api.deck.updateDecks`, `api.game.getGame`/`getRandomGame`.

---

## Core / QoS / Network

Low-level utilities + transport. Most C++ utils are intentionally replaced by .NET BCL. Genuine gaps: `QoS::Server`, `Scheduler`.

| C++ class | C++ file:line | C# counterpart | Status | Notes |
|---|---|---|---|---|
| `Scheduler` (AddTask/CancelTask) | `Core/Async/Scheduler.h:47` | — | ❌ | only hardcoded 50ms `Task.Delay` loop in `RakNetServer.ExecuteAsync`; no cancellable timed-task primitive |
| `Task`/`TaskComparator` | `Core/Async/Scheduler.h:17,40` | `System.Threading.Tasks` | N/A | BCL |
| `DataBuffer` | `Core/IO/DataBuffer.h:12` | `BigEndianExtensions` on BinaryReader/Writer | ⚠️ | BE/LE covered; TDF int codec in `Blaze/Extensions` |
| `utils::extended`/timestamp (color log) | `Core/Utils/Log.h:12` | `Util/Logger.cs` | ⚠️ | no ANSI color/timestamps |
| `utils::json` | `Core/Utils/JSON.h:17` | `System.Text.Json` | N/A | BCL |
| `utils::random` | `Core/Utils/Functions.h:183` | scattered `System.Random` | ❌ | no typed `random::get<T>` helper |
| `utils::hash_id` (FNV-1a) | `Core/Utils/Functions.h:167` | — | ❌ | not exposed; AssetData.Parser keys by string |
| `utils::enum_wrapper/helper` | `Core/Utils/Functions.h:210,287` | `[Flags]` enums | N/A | BCL |
| string/time utils | `Core/Utils/Functions.h:44-60` | BCL (Split/StringComparer/DateTimeOffset) | N/A | |
| `utils::xml_*node` | `Core/Utils/Functions.h:136-163` | `Util/XmlHelper.cs` | ⚠️ | serialize-only; no per-node typed get/add |
| `utils::net::resolve_ip` | `Core/Utils/Net.h:13` | — | ❌ | no DNS resolution wrapper |
| `Version.h` | `Core/Base/Version.h:1` | `ServerConfigOptions` | ⚠️ | tracks game version, not server self-version |
| `QoS::Server` | `QoS/Server.h:11` | — | ❌ | no UDP QoS responder; may block login/matchmaking |
| `Network::Client` (abstract) | `Network/Client.h:14` | Blaze/RakNet clients | ⚠️ | no shared base |

**Top 5:** `QoS::Server`, `Scheduler`, `utils::hash_id` (FNV-1a), `utils::random` typed helper, `utils::xml_*node` typed overloads.

---

## Deep-dive doc families

Individual rows above are expanded class/command/endpoint-by-detail in:

- [`components/`](../protocol/components/README.md) — per-Blaze-component command tables (expands the [Blaze](#blaze) section).
- [`data-model/`](../data-model/README.md) — per-entity field tables + persistence (expands [SporeNet](#sporenet)).
- [`http/`](../protocol/http/README.md) — complete REST endpoint catalog (supersedes the selected-rows [HTTP](#http) table).
- `systems/` — Game-module subsystem deep-dives (not yet authored; Opus-grade — see ROADMAP M7).

## How to keep this in sync

- This is a **snapshot** (2026-05-25). When a row's status changes (a port lands), edit the row + the executive summary coverage estimate in the same turn.
- Deep-dive docs (`components/`, `systems/`, `data-model/`, `http/`) expand individual rows. When a deep-dive lands, link it from the relevant row.
- Re-run the 6-agent fan-out after major porting milestones to refresh coverage estimates.
