# Blaze: Create-Game → Enter-Game flow (single-player Dungeon)

Maps the GameManager + Rooms Blaze flow from "create the dedicated game" through "player in game", verified against the working C++ binary log (`…/DarksporeBin/Server/output.txt`) and the C# rolling log (`ReCap.Server/bin/.../logs/recap-<date>.log`). Built while chasing the post-deploy fall-back (the client re-issues `joinRoom` ~1s after deploy, then crashes — DIVERGENCE_LEDGER D-009 / OBJECTS_OBJECTIVES_SYSTEM.md).

## Verified sequence (C++ working log, matches C# command-for-command)

```
client → resetDedicatedServer (GameManager 0x19)
server → reply {GID}                              (C++ WriteJoinGame; C# JoinGameResponse{GID,JGS})
server → NotifyGameCreated (0x0F)
server → NotifyGameSetup   (0x14)   GAME.GSTA = Initializing(1)
server → NotifyPlayerJoining (0x15)
client → finalizeGameCreation (GameManager 0x0F)
server → reply {GID}                              (C++ WriteJoinGame → GID)
server → NotifyGameStateChange       (0x64)  GSTA = InGame(0x83)
server → NotifyGamePlayerStateChange (0x74)  STAT = Connected(4)
server → NotifyPlayerJoinCompleted   (0x1E)
```

The command sequence and the key enum values (`GameState.InGame=0x83`, `PlayerState.Connected=4`, `GameSetupReason=0`) are **identical** C++↔C#. So the fall-back is NOT a missing GameManager command or a wrong game-state value.

## Field-level divergences (source-diff, ranked) — mostly fidelity, NOT the fall-back

> **Caveat:** the client demonstrably PASSES create-game (it reaches the dungeon and deploys). The fall-back happens ~1s AFTER a successful deploy. So these create-stage divergences are real fidelity gaps but are unlikely to be the crash cause. Verify each against C++ before trusting.

| # | Field | C++ | C# | Status |
|---|---|---|---|---|
| 6 | `finalizeGameCreation` reply body | `WriteJoinGame` → `{GID}` (GameManagerComponent.cpp:1105) | was **empty** | **FIXED** — now replies `JoinGameResponse{GameId}` |
| 3 | `NotifyGameSetup.GAME.HNET` encoding | plain struct in list (Functions.cpp:647-663, `#if 1`) | `NetworkAddress` **union**-wrapped | open — needs TDF-encoder check |
| 4 | `NotifyGameSetup.PROS[0].SID` (host slot) | `0` (cpp:1208) | default `0xFF` (not set) | open |
| 5 | `finalizeGameCreation` → `Game::GameManager::StartGame(gid)` | called (cpp:1102) | not called | open — read StartGame to see if it has wire-visible effect |
| 2 | `NotifyGameSetup.GAME.MATR` | omitted (Functions.cpp:668) | echoed from client if non-empty | low |
| — | NRES inverted-write semantics | `resetable ? 0 : 1` (Functions.cpp:678) | echoes client `NRES` | verify on wire |

## The real open question (the fall-back)

The client creates the game, enters the dungeon, deploys, runs ~1s, then sends a mid-game `joinRoom` (Rooms 0x14) and crashes on a null HUD-subview movie (`ClientUI::ViewManager_UpdatePerFrame` → `[subview+0x20]` null). The C++ client never re-joins mid-game; it plays to its frontier (`UseCharacterAbility`). Matching the wire content (LPU, objectives, hero objects, 2866 level objects, the GameManager sequence) has NOT stopped it. This points to a client-side timeout/condition not determined by the wire content matched so far — a candidate for the Ghidra runtime debugger (what gates the per-frame subview switch / what the client waits for in the ~1s post-deploy window). The C++ post-deploy LIVING stream (ObjectPlayerMove/Teleport/InteractableDataUpdate per-tick — Phase 2) is the remaining untested wire difference.
