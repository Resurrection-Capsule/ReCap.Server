# GameManager Component — 0x04

Manages dedicated-server game sessions: creation, player join/leave, matchmaking, and mesh topology, on the Blaze lobby connection (port 42125).

---

## Request/response commands

| Command name | Cmd ID | Direction | C++ handler | C# handler | Status |
|---|---|---|---|---|---|
| createGame | 0x01 | C→S | `Blaze/Component/GameManagerComponent.cpp:970` | — | ❌ |
| destroyGame | 0x02 | C→S | `Blaze/Component/GameManagerComponent.cpp:984` | — | ❌ |
| advanceGameState | 0x03 | C→S | — (enum only) | — | ❌ |
| setGameSettings | 0x04 | C→S | — (enum only) | — | ❌ |
| setPlayerCapacity | 0x05 | C→S | — (enum only) | — | ❌ |
| setGameAttributes | 0x07 | C→S | — (enum only) | — | ❌ |
| setPlayerAttributes | 0x08 | C→S | `Blaze/Component/GameManagerComponent.cpp:997` | — | ❌ |
| joinGame | 0x09 | C→S | `Blaze/Component/GameManagerComponent.cpp:1002` | — | ❌ |
| removePlayer | 0x0B | C→S | `Blaze/Component/GameManagerComponent.cpp:1044` | — | ❌ |
| startMatchmaking | 0x0D | C→S | `Blaze/Component/GameManagerComponent.cpp:1066` | — | ❌ |
| cancelMatchmaking | 0x0E | C→S | `Blaze/Component/GameManagerComponent.cpp:1075` | — | ❌ |
| finalizeGameCreation | 0x0F | C→S | `Blaze/Component/GameManagerComponent.cpp:1087` | `Adapters/Blaze/Component/GameManager/GameManagerComponent.cs:40` | ✅ |
| resetDedicatedServer | 0x19 | C→S | `Blaze/Component/GameManagerComponent.cpp:1115` | `Adapters/Blaze/Component/GameManager/GameManagerComponent.cs:71` | ✅ |
| updateMeshConnection | 0x1D | C→S | `Blaze/Component/GameManagerComponent.cpp:1246` | `Adapters/Blaze/Component/GameManager/GameManagerComponent.cs:203` | ✅ |
| listGames | 0x11 | C→S | — (enum only) | — | ❌ |
| getGameListSnapshot | 0x64 | C→S | — (enum only) | — | ❌ |
| getGameListSubscription | 0x65 | C→S | — (enum only) | — | ❌ |
| getFullGameData | 0x67 | C→S | — (enum only) | — | ❌ |
| getMatchmakingConfig | 0x68 | C→S | — (enum only) | — | ❌ |
| migrateGame | 0x17 | C→S | — (enum only) | — | ❌ |
| banPlayer | 0x1B | C→S | — (enum only) | — | ❌ |
| updateGameName | 0x27 | C→S | — (enum only) | — | ❌ |

## Notifications sent (S→C)

| Notification name | Notify ID | Direction | C++ sender | C# sender | Status |
|---|---|---|---|---|---|
| NotifyGameCreated | 0x0F | S→C | `Blaze/Component/GameManagerComponent.cpp:680` | `GameManagerComponent.cs:191` | ✅ |
| NotifyGameSetup | 0x14 | S→C | `Blaze/Component/GameManagerComponent.cpp:695` | `GameManagerComponent.cs:192` | ✅ |
| NotifyPlayerJoining | 0x15 | S→C | `Blaze/Component/GameManagerComponent.cpp:864` | `GameManagerComponent.cs:194` | ✅ |
| NotifyPlayerJoinCompleted | 0x1E | S→C | `Blaze/Component/GameManagerComponent.cpp:880` | `GameManagerComponent.cs:62,224` | ✅ |
| NotifyPlayerRemoved | 0x28 | S→C | `Blaze/Component/GameManagerComponent.cpp:888` | — | ❌ |
| NotifyGameStateChange | 0x64 | S→C | `Blaze/Component/GameManagerComponent.cpp:906` | `GameManagerComponent.cs:53` | ✅ |
| NotifyGamePlayerStateChange | 0x74 | S→C | `Blaze/Component/GameManagerComponent.cpp:937` | `GameManagerComponent.cs:55,215` | ✅ |
| NotifyMatchmakingFailed | 0x0A | S→C | `Blaze/Component/GameManagerComponent.cpp:646` | — | ❌ |
| NotifyMatchmakingAsyncStatus | 0x0C | S→C | `Blaze/Component/GameManagerComponent.cpp:662` | — | ❌ |
| NotifyGameRemoved | 0x10 | S→C | `Blaze/Component/GameManagerComponent.cpp:687` | — | ❌ |
| NotifyHostMigrationCompleted | 0x3C | S→C | — (enum only) | — | ❌ |
| NotifyPlatformHostInitialized | 0x47 | S→C | `Blaze/Component/GameManagerComponent.cpp:898` | — | ❌ |
| NotifyGameReset | 0x70 | S→C | `Blaze/Component/GameManagerComponent.cpp:914` | — | ❌ |
| NotifyGameSessionUpdated | 0x73 | S→C | `Blaze/Component/GameManagerComponent.cpp:928` | — | ❌ |
| NotifyCreateDynamicDedicatedServerGame | 0xDC | S→C | `Blaze/Component/GameManagerComponent.cpp:946` | — | ❌ |

---

## Key TDF fields — resetDedicatedServer (0x19) / NotifyGameSetup (0x14)

This command is the critical path: client sends it to request a game session; server responds with GID and fires `NotifyGameSetup`.

**Request (CreateGameRequest):**

| Tag | Type | Description |
|---|---|---|
| `ATTR` | map&lt;string,string&gt; | Game attributes (includes `SelectedDifficulty`, `GameOwnerId`, etc.) |
| `GNAM` | string | Game name |
| `GSET` | u32 | Game settings bitmask (256+4=standard chain, 1024=pvp) |
| `GTYP` | string | Game type name |
| `HNET` | list&lt;struct&gt; | Host network address list (IpPairAddress) |
| `NTOP` | enum | Network topology (1=ClientServerDedicated) |
| `PRES` | enum | Presence mode (1=Standard) |
| `VSTR` | string | Version string |

**Response:**

| Tag | Type | Description |
|---|---|---|
| `GID` | u32 | Assigned game ID |

**NotifyGameSetup (0x14) — GAME struct (ReplicatedGameData):**

| Tag | Type | Description |
|---|---|---|
| `GID` | u32 | Game ID |
| `GNAM` | string | Game name |
| `GPVH` | u64 | Game protocol version hash (hardcoded `0xABABABAB`) |
| `GSET` | u32 | Game settings |
| `GSID` | u64 | Game session ID (hardcoded `0xCDCDCDCD`) |
| `GSTA` | enum | Game state (1=Initializing) |
| `HNET` | list&lt;struct&gt; | Host network address |
| `HSES` | u32 | Host session ID (hardcoded `13666`) |
| `NQOS` | struct | Network QoS (DBPS, NATT, UBPS) |
| `NTOP` | enum | Network topology |
| `PHST` | struct | Platform host (HPID=userId, HSLT=0) |
| `UUID` | string | Hardcoded `"71bc4bdb-82ec-494d-8d75-ca5123b827ac"` |

---

## Porting gaps

- `createGame` (0x01), `destroyGame` (0x02), `joinGame` (0x09), `removePlayer` (0x0B), `startMatchmaking` (0x0D), `cancelMatchmaking` (0x0E) all have C++ implementations but no C# equivalents — client reaches these via multiplayer paths not yet exercised in single-player.
- `NotifyPlayerRemoved` (0x28) and `NotifyMatchmakingFailed` (0x0A) are not sent from C#; required for proper disconnect handling.
- C++ `ResetDedicatedServer` loads the level via `chainData.SetLevelByIndex`; C# version does not mirror this — level loading is handled independently via the RakNet flow.
- `NotifyCreateDynamicDedicatedServerGame` (0xDC) path is entirely absent in C#; only relevant for dedicated-server topology.
