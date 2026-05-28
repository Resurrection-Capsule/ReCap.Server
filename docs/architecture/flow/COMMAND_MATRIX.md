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
| 0x9C | ActionCommandMsgs | `OnActionCommandMsgs` (688) | ⚠️ dispatched but **parse misaligned**, no response (task #11) |
| 0xAC | ChainPlayerMsgs | `OnChainPlayerMsgs` (988) | ✅ handled (Game) |
| 0xC2 | CrystalDragMessage | `OnCrystalDragMessage` (1026) | ❌ **not handled** (catalyst drag/swap) |
| 0xCB | LootDropMessage | `OnLootDropMessage` (1093) | ❌ not handled (C++ itself is a stub) |
| 0xCC | DebugPing | `OnDebugPing` (1136) | ✅ handled (Game) |

**ActionCommandMsgs (0x9C) sub-commands** (dispatched inside `OnActionCommandMsgs`):
Movement=3, StopMovement=4, SwitchCharacter=5, UseCharacterAbility=7, UseSquadAbility=8,
CatalystPickup=9, Cancel=10, UseInteractableObject=11, Dance=12, Taunt=13.
→ Movement/Stop/Switch are pure C# (locomotion). UseAbility/SquadAbility = **Lua phase**.

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
| 0x9C | ActionCommandMsgs | C→S | ✅ | OnActionCommandMsgs | ⚠️ parse misaligned, no ObjectPlayerMove reply |
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
| 0xC2 | CrystalDragMessage | C→S | ❌ | OnCrystalDragMessage | **inbound gap** — catalyst drag/swap |
| 0xC3 | CrystalMessage | S→C | — | SendCrystalMessage | catalyst slot update |
| 0xC4 | KillRacePlayerMsgs | — | — | **unimpl** | mode |
| 0xC5 | KillRaceLobbyMsgs | — | — | **unimpl** | mode |
| 0xC6 | KillRaceGameMsgs | S→C | — | SendKillRaceGame | never wired |
| 0xC7 | KillRaceResultsMsgs | — | — | **unimpl** | mode |
| 0xC8 | TutorialGameMsgs | S→C | — | SendTutorial | bool / xp |
| 0xC9 | CinematicMsgs | S→C | — | SendCinematic | cinematic params |
| 0xCA | ObjectiveAdd | S→C | — | SendObjectiveAdd | Objective.WriteTo |
| 0xCB | LootDropMessage | C→S | ❌ | OnLootDropMessage | C++ itself stubbed (hex dump only) |
| 0xCC | DebugPing | C↔S | ✅ | OnDebugPing / SendDebugPing | state-machine driver |

> Note: 0x9D is absent from the enum (0x9C → 0x9E).

---

## Gaps & next steps

**Inbound (C→S) gaps — the only ones that block gameplay:**
- **0x9C ActionCommandMsgs** — dispatched, but our `ReadFrom` reads `ObjectId` first while
  the real layout has the type byte right after the packet id (objectId at +0x09, position
  at +0x0D). We also never reply with `0x91 ObjectPlayerMove`. → **task #11** (movement first;
  abilities = Lua phase).
- **0xC2 CrystalDragMessage** — unhandled. Needed for catalyst slot drag/swap in-game.
- 0xCB LootDropMessage — C++ is itself a stub; low priority.

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
