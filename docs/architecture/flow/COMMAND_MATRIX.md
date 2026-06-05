# RakNet Command Matrix

Survey of the gameplay (RakNet, port 42000) `PacketID` / `kGms*` protocol: what is
mapped in ReCap, what the C++ reference does, and what the client expects.

**Sources**
- **C#** — `ReCap.Server/Adapters/RakNet/PacketType.cs` (enum, complete 0x7F–0xCC),
  `Packets/` (implemented classes), `PacketActivator.cs` (inbound parse),
  `Game.HandlePacket` + `RakNetServer` (inbound dispatch).
- **C++ (ground truth)** — `ReCapCpp/.../RakNet/Server.cpp`; dispatch switch
  `Server::ParseSporeNetPackets` (Server.cpp:458).
- **Client (Darkspore.exe in Ghidra)** — `nSporeNet` transport; `kGms*` name table at
  `0x01036410+`; per-message `OnGms*` handlers. Confirms our enum names 1:1.

## Direction legend
- **C→S** client→server (server must *handle* it). These are the gaps that matter most.
- **S→C** server→client (server *sends* it).

---

## Client→Server — the only inbound the C++ server handles

The C++ dispatch switch (Server.cpp:458–498) handles **exactly 7** inbound IDs; every
other client packet hits `default` and is dropped.

| ID | Name | C++ handler | ReCap status |
|----|------|-------------|--------------|
| 0x7F | HelloPlayerRequest | `OnHelloPlayerRequest` (596) | ✅ handled (RakNetServer) |
| 0x88 | PlayerStatusUpdate | `OnPlayerStatusUpdate` (643) | ✅ handled (Game) |
| 0x9C | ActionCommandMsgs | `OnActionCommandMsgs` (688) | ✅ all client-emitted types handled (D-020); abilities/overdrive = ack stubs pending Simulation |
| 0xAC | ChainPlayerMsgs | `OnChainPlayerMsgs` (988) | ✅ handled (Game) |
| 0xC2 | CrystalDragMessage | `OnCrystalDragMessage` (1026) | ✅ handled (D-021; reject-only until loot phase fills the grid) |
| 0xCB | LootDropMessage | `OnLootDropMessage` (1093) | ✅ raw-capture handler (layout unknown — C++ is a hexdump stub; map when client emits) |
| 0xCC | DebugPing | `OnDebugPing` (1136) | ✅ handled (Game) |

**ActionCommandMsgs (0x9C) sub-commands** (dispatched inside `OnActionCommandMsgs`; client
ground-truth sweep 2026-06-05, VERIFIED_FACTS "ActionCommand contract"):
Movement=3, StopMovement=4, SwitchCharacter=5, **Overdrive=6 (client-only, not in C++)**,
UseCharacterAbility=7, UseSquadAbility=8, CatalystPickup=9, Cancel=10,
UseInteractableObject=11, Dance=12, Taunt=13. Types 1/2 = inert (no client emit path).
→ All handled in C# (D-018/D-019/D-020). Enqueue-path types (5/6/9/11/12/13) carry a
rolling commandStamp at header +0x01 and arm a 3s client lock — server must ack with
ActionCommandResponse type=2 echoing the stamp. Ability/Overdrive effects = **Simulation phase**.

---

## Full matrix (0x7F–0xCC)

`C# class` = packet class exists in `Packets/`. `C# disp` = inbound parse+dispatch wired.

| ID | Name | Dir | C# class | C++ sender/handler | Notes |
|----|------|-----|----------|--------------------|-------|
| 0x7F | HelloPlayerRequest | C→S | ✅ | OnHelloPlayerRequest | blazeId, sets catalysts, state=Spaceship |
| 0x80 | HelloPlayer | S→C | ✅ | SendHelloPlayer | type, mId, IP, port (8B) |
| 0x81 | ReconnectPlayer | S→C | — | SendReconnectPlayer | u32 gameState |
| 0x82 | Connected | S→C | ✅ | SendConnected | no body |
| 0x83 | Goodbye | — | — | **unimpl** | — |
| 0x84 | PlayerJoined | S→C | ✅ | SendPlayerJoined | u8 clientId |
| 0x85 | PartyMergeComplete | S→C | ✅ | SendPartyMergeComplete | u64 time; triggers client DebugPing |
| 0x86 | PlayerDeparted | S→C | — | SendPlayerDeparted | u8 clientId |
| 0x87 | VoteKickStarted | — | — | **unimpl** | — |
| 0x88 | PlayerStatusUpdate | C→S | ✅ | OnPlayerStatusUpdate | status 2/4/8/0x20; LPU after |
| 0x89 | GameAborted | — | — | **unimpl** | — |
| 0x8A | GameState | S→C | ✅ | SendGameState | broadcast every tick |
| 0x8B | DirectorState | S→C | ✅ | SendDirectorState | cAIDirector raw 0x4D0 |
| 0x8C | ObjectCreate | S→C | ✅ | SendObjectCreate | createData + object reflection |
| 0x8D | ObjectUpdate | S→C | ✅ | SendObjectUpdate | object reflection |
| 0x8E | ObjectDelete | S→C | — | SendObjectDelete | u32 objId |
| 0x8F | ObjectJump | S→C | — | SendObjectJump | objId + vecs |
| 0x90 | ObjectTeleport | S→C | — | SendObjectTeleport | objId + pos + quat |
| 0x91 | ObjectPlayerMove | S→C | ✅ | SendObjectPlayerMove | locomotion goal/facing/etc. (movement reply) |
| 0x92 | ForcePhysicsUpdate | S→C | — | SendForcePhysicsUpdate | objId + pos/euler/vel |
| 0x93 | PhysicsChanged | S→C | — | SendPhysicsChanged | objId + bool collision |
| 0x94 | LocomotionDataUpdate | S→C | ✅ | SendLocomotionDataUpdate | objId + LocomotionData raw |
| 0x95 | LocomotionDataUnreliableUpdate | S→C | — | Send… | objId + vec3 pos |
| 0x96 | AttributeDataUpdate | S→C | ✅ | SendAttributeDataUpdate | objId + Attributes reflection |
| 0x97 | CombatantDataUpdate | S→C | ✅ | SendCombatantDataUpdate | objId + HP/MP reflection |
| 0x98 | InteractableDataUpdate | S→C | — | SendInteractableDataUpdate | raw; DBG-gated |
| 0x99 | AgentBlackboardUpdate | S→C | — | SendAgentBlackboardUpdate | reflection |
| 0x9A | LootDataUpdate | S→C | — | SendLootDataUpdate | DBG=false (dead) |
| 0x9B | ServerEvent | S→C | — | SendServerEvent | DBG=false (dead) |
| 0x9C | ActionCommandMsgs | C→S | ✅ | OnActionCommandMsgs | ✅ all client types handled (D-015..D-020) |
| 0x9E | PlayerDamage | — | — | **unimpl** | — |
| 0x9F | LootSpawned | — | — | **unimpl** | — |
| 0xA0 | LootAcquired | — | — | **unimpl** | — |
| 0xA1 | LabsPlayerUpdate | S→C | ✅ | SendLabsPlayerUpdate | player + chars + catalysts |
| 0xA2 | ModifierCreated | S→C | — | SendModifierCreated | buff/debuff create |
| 0xA3 | ModifierUpdated | S→C | — | SendModifierUpdated | stack/timestamp |
| 0xA4 | ModifierDeleted | S→C | — | SendModifierDeleted | objId + modId |
| 0xA5 | SetAnimationState | S→C | — | SendAnimationState | anim state |
| 0xA6 | SetObjectGfxState | S→C | — | SendObjectGfxState | gfx state |
| 0xA7 | PlayerCharacterDeploy | S→C | ✅ | SendPlayerCharacterDeploy | playerId + creatureIndex + objId (9B) |
| 0xA8 | ActionCommandResponse | S→C | ✅ | SendActionCommandResponse | type / ability cooldown overload |
| 0xA9 | ChainVoteMsgs | S→C | ✅ | SendChainVoteMessages | 0=ChainData 0x151 LE, 1=secs, 2=stayInParty |
| 0xAA | ChainLevelResultsMsgs | — | — | **unimpl** | — |
| 0xAB | ChainCashOutMsgs | S→C | — | SendChainCashOutMessages | ⚠️ C++ writes wrong ID (0xA9) |
| 0xAC | ChainPlayerMsgs | C→S | ✅ | OnChainPlayerMsgs | byteCount 1/2/6 (vote→PrepareGameStart) |
| 0xAD | ChainGameMsgs | S→C | — | SendChainGame | fade / mission-failed |
| 0xAE | ChainGameOverMsgs | — | — | **unimpl** | — |
| 0xAF | QuickGameMsgs | S→C | ✅ | SendQuickGame | bool reset |
| 0xB0 | GamePrepareForStart | S→C | ✅ | SendGamePrepareForStart | level/markerset/1/levelIndex (17B) |
| 0xB1 | GameStart | S→C | ✅ | SendGameStart | u32 levelIndex (5B) |
| 0xB2 | CheatMessage… | — | — | **unimpl** | dev-only stub |
| 0xB3 | ArenaPlayerMsgs | C→S? | — | **unimpl** | arena mode |
| 0xB4 | ArenaLobbyMsgs | — | — | **unimpl** | arena |
| 0xB5 | ArenaGameMsgs | S→C | — | SendArenaGameMessages | never wired |
| 0xB6 | ArenaResultsMsgs | — | — | **unimpl** | arena |
| 0xB7 | ObjectivesInitForLevel | S→C | ✅ | SendObjectivesInitForLevel | count + Objective×N (56B each) |
| 0xB8 | ObjectiveUpdated | S→C | ✅ | SendObjectiveUpdate | per-objective progress |
| 0xB9 | ObjectivesComplete | S→C | ✅ | SendObjectivesComplete | + medal bitmask |
| 0xBA | CombatEvent | S→C | — | SendCombatEvent | reflection |
| 0xBB | JuggernautPlayerMsgs | — | — | **unimpl** | mode |
| 0xBC | JuggernautLobbyMsgs | — | — | **unimpl** | mode |
| 0xBD | JuggernautGameMsgs | S→C | — | SendJuggernautGame | never wired |
| 0xBE | JuggernautResultsMsgs | — | — | **unimpl** | mode |
| 0xBF | ReloadLevel | S→C | — | SendReloadLevel | no body |
| 0xC0 | GravityForceUpdate | — | — | **unimpl** | — |
| 0xC1 | CooldownUpdate | S→C | — | SendCooldownUpdate | ability cooldowns |
| 0xC2 | CrystalDragMessage | C→S | ✅ | OnCrystalDragMessage | ✅ D-021 (24B parse; reject until loot) |
| 0xC3 | CrystalMessage | S→C | ✅ | SendCrystalMessage | ✅ D-021 (fixed 29B, client @0x0053f700) |
| 0xC4 | KillRacePlayerMsgs | — | — | **unimpl** | mode |
| 0xC5 | KillRaceLobbyMsgs | — | — | **unimpl** | mode |
| 0xC6 | KillRaceGameMsgs | S→C | — | SendKillRaceGame | never wired |
| 0xC7 | KillRaceResultsMsgs | — | — | **unimpl** | mode |
| 0xC8 | TutorialGameMsgs | S→C | — | SendTutorial | bool / xp |
| 0xC9 | CinematicMsgs | S→C | — | SendCinematic | cinematic params |
| 0xCA | ObjectiveAdd | S→C | — | SendObjectiveAdd | Objective.WriteTo |
| 0xCB | LootDropMessage | C→S | ✅ | OnLootDropMessage | raw-capture handler (C++ itself stubbed; layout unmapped) |
| 0xCC | DebugPing | C↔S | ✅ | OnDebugPing / SendDebugPing | state-machine driver |

> Note: 0x9D is absent from the enum (0x9C → 0x9E).

---

## Client-side handler map (Ghidra, 2026-06-05 — complete in-game dispatcher sweep)

Source: kGms name table @`0x0118b488` (pairs `{char* name, u32 internal id}`, ids 0–77)
+ the in-game dispatcher @`0x0053fbb0` (switch on internal id−6, byte-table `0x0053feb4`,
jump-table `0x0053fe10`, 40 cases). All handlers renamed + plate-commented in the Ghidra
project as `ClientNet::OnGms<Name>`.

✅ **Remap rule SOLVED (2026-06-05):** `wire opcode = 0x7F + POSITION in the kGms name
table @0x118b488`; the table's u32 value = the internal dispatch id (what `GetType()`
returns and what the dispatchers switch on). The table position order matches the C++/C#
`PacketID`/`PacketType` enum — verified position-by-position against wire-anchored ids
(0x8C, 0x90, 0x91, 0x9C, 0xA1, 0xA7, 0xAF-0xB1, 0xB7, 0xB8, 0xCC).

**Enum bug found+fixed via this rule:** C++ `Types.h` (and old C# `PacketType.cs`)
skipped 0x9D and shifted the next names +1. Client truth: `0x9D=PlayerDamage,
0x9E=LootSpawned, 0x9F=LootAcquired, 0xA0=SystemMessage` (SystemMessage was missing
entirely). C# fixed 2026-06-05; nothing sent in that range yet, so no wire impact.

**Handler registration mechanism:** handlers are `tTransportMessageFunctor{vtbl, fn,
this}` objects registered per kGms internal id on the msg-manager singleton.
`nSporeNet::cClientSession::ConnectAndRegisterMessages` @0x00a93353 registers the
connection-phase ids (1 HelloPlayer, 3 Connected, 5 PlayerJoined, 7 PlayerDeparted);
`cSporeOnline_Common`'s functor is the in-game dispatcher below (covers 40 ids,
default=drop); Chain*/Arena/mode messages are consumed by per-game-state listeners
registered on state entry (heap — map via runtime trace or per-state OnEnter sweep).

**GameSimulator (embedded server!):** `FUN_009c6150` @0x009c6150 ("SimulatorDebugPing"
registry entry) is a debug-path dispatcher that handles the **client→server** ids
in-process: 9 PlayerStatusUpdate → 0x009c3020, 29 ActionCommandMsgs → **0x009c5b40**,
67 CrystalDrag → 0x009c2f60, 76 LootDrop (stub), 77 DebugPing → 0x009c5340. The client
ships the original server-side command handling — `0x009c5b40` is ground truth for what
the real server did with ActionCommands (richer than the C++ reference; deep-dive
pending).

| kGms id | Name | Client handler | Note |
|---------|------|----------------|------|
| 6  | PartyMergeComplete | 0x004e8790 | |
| 8  | VoteKickStarted | 0x0053cd30 | |
| 11 | GameState | 0x0053ca60 | per-tick (wire 0x8A) |
| 12 | ObjectCreate | 0x0053f550 | mapped earlier (D-009) |
| 13 | ObjectUpdate | 0x0053dd00 | mapped earlier |
| 14 | ObjectDelete | 0x0053ddc0 | |
| 15 | PlayerCharacterDeploy | 0x0053fa90 | |
| 16 | ObjectTeleport | 0x0053e0e0 | UNGATED — see CLIENT_MOVEMENT_CONTRACT.md |
| 17 | ObjectJump | 0x0053dee0 | |
| 19 | ForcePhysicsUpdate | 0x0053e380 | 40B = C++ SendForcePhysicsUpdate |
| 18 | ObjectPlayerMove | 0x0053e1c0 | LOCAL-HERO GATE — see CLIENT_MOVEMENT_CONTRACT.md |
| 20 | PhysicsChanged | 0x0053e490 | |
| 21 | LocomotionDataUpdate | 0x0053e6f0 | reflection-encoded |
| 22 | LocomotionDataUnreliableUpdate | 0x0053e600 | UNGATED smooth-move channel |
| 23 | AttributeDataUpdate | 0x0053e7c0 | wire 0x96 |
| 24 | CombatantDataUpdate | 0x0053e890 | wire 0x97 |
| 25 | InteractableDataUpdate | 0x0053e960 | wire 0x98 |
| 26 | AgentBlackboardUpdate | 0x0053ea30 | wire 0x99 |
| 27 | LootDataUpdate | 0x0053eb00 | |
| 28 | ServerEvent | 0x0053ec80 | |
| 30 | ActionCommandResponse | 0x0053cb10 | reply to 0x9C — candidate unblock for the deferred command path (+3000ms deadline) |
| 35 | LabsPlayerUpdate | 0x0053ebf0 | wire 0xA1 |
| 36/37/38 | ModifierCreated/Updated/Deleted | 0x0053edd0 / 0x0053ee80 / 0x0053ef30 | |
| 39 | SetAnimationState | 0x0053efe0 | |
| 40 | SetObjectGfxState | 0x0053f170 | |
| 48 | GamePrepareForStart | 0x0053cb80 | |
| 49 | GameStart | 0x0053cc40 | |
| 55 | DirectorState | 0x0053dcd0 | |
| 56 | ObjectivesInitForLevel | 0x0053c820 | |
| 57 | ObjectivesComplete | 0x0053bc50 | |
| 58 | ObjectiveUpdated | 0x0053bb10 | |
| 63 | CombatEvent | 0x0053ed50 | |
| 64 | ReloadLevel | 0x0053cce0 | |
| 65 | GravityForceUpdate | 0x0053f240 | |
| 66 | CooldownUpdate | 0x0053f310 | |
| 68 | CrystalMessage | 0x0053f700 | |
| 75 | ObjectiveAdd | 0x0053c970 | |
| 77 | DebugPing | 0x0053d840 | |

**Not in this dispatcher** (other receive paths / client→server only): HelloReq(0),
HelloPlayer(1, @0x00a93d50), ReconnectPlayer(2), Connected(3, @0x00a93b50), Goodbye(4),
PlayerJoined(5, @0x00a93f50), PlayerDeparted(7, @0x00a94040), PlayerStatusUpdate(9),
GameAborted(10), ActionCommandMsgs(29, C→S), PlayerDamage(31), LootSpawned(32),
LootAcquired(33), SystemMessage(34), ChainPlayerMsgs(41), ChainVote(42),
ChainLevelResults(43), ChainCashOut(44), ChainGame(45), ChainGameOver(46), QuickGame(47),
Cheat(50), Arena(51-54), Juggernaut(59-62), CrystalDragMessage(67, C→S),
KillRace(69-72), TutorialGame(73), Cinematic(74), LootDropMessage(76, C→S). The
connection/lobby-phase ones live in the `0x00a9xxxx` transport layer; the Chain*/mode
messages likely have a second dispatcher (unmapped — next sweep target).

## Gaps & next steps

**Inbound (C→S) gaps — the only ones that block gameplay:**
- ~~0x9C ActionCommandMsgs~~ — DONE through D-020 (2026-06-05): parse fixed, all client-emitted
  types handled, command-lock ack (0xA8 type=2 stamp echo) in place. Remaining depth = real
  ability/overdrive resolution (Simulation phase) + catalyst loot effect (loot phase).
- ~~0xC2 CrystalDragMessage~~ — DONE (D-021): parse + 0xC3 reject (29B); swap/drop depth = loot phase.
- ~~0xCB LootDropMessage~~ — raw-capture handler (layout unknown; C++ is a hexdump stub — hex-log ours the same way and map via Ghidra when it fires).
- **Inbound layer is now 100%** — every C→S packet the client emits is parsed and answered.

**Outbound (S→C) not yet sent but likely needed soon:**
- 0x8E ObjectDelete, 0x90 ObjectTeleport, 0xA5 SetAnimationState — object/combat lifecycle.
- 0xA2–0xA4 Modifier*, 0xBA CombatEvent, 0xC1 CooldownUpdate — combat feedback (Lua phase).
- 0xC3 CrystalMessage — reply to CrystalDrag.

**C++ reference quirks to NOT copy blindly (dalkon hardcodes/bugs):**
- 0xAB SendChainCashOutMessages writes the wrong packet id (0xA9).
- `DBG_SEND_*` static flags disable ServerEvent/LootDataUpdate in the reference.

**Lua boundary:** movement, object/locomotion, deploy, objectives are pure C#. Ability
execution (UseCharacterAbility/UseSquadAbility, modifiers, cooldowns, combat events) is
Lua-driven in the retail engine — that is a later phase, not required for basic gameplay.
