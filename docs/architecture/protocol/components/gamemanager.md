# GameManager Component — 0x04

Drives dedicated-server game-session lifecycle on the Blaze lobby connection (port 42125):
game creation, player join/remove, mesh-connection confirmation, and matchmaking. The critical
single-player path is `resetDedicatedServer` → `NotifyGameSetup` → `NotifyPlayerJoining` →
`finalizeGameCreation` → `updateMeshConnection`. The `NotifyGameSetup` packet carries the full
`ReplicatedGameData` struct that the client uses to bind its GFx deck/player UI — malformed
fields here are the suspected cause of the current Dungeon-entry client crash.

Audit pass 2026-05-29: verified against C++
`Blaze/Component/GameManagerComponent.cpp` (handlers + notification writers),
`Blaze/Component/GameManagerComponent.h` (dispatch table),
`Blaze/Functions.cpp` (TDF struct writers: `ReplicatedGameData::Write` :614,
`ReplicatedGamePlayer::Write` :579, `NetworkQosData::Write` :147, `HostInfo::Write` :260),
C# port `Adapters/Blaze/Component/GameManager/GameManagerComponent.cs`, and the GOOD/BAD
annotated wire logs embedded in `GameManagerComponent.cpp:20-177`. Tags: `[V]` = verified in
cited source, `[?]` = unverified / needs Ghidra. Ghidra **not reachable** this pass.

---

## Verified single-player Blaze flow `[V]`

From WORKING log comment (`GameManagerComponent.cpp:20`):

1. Client → `resetDedicatedServer` (0x19) — carries `CreateGameRequest` body
2. Server → reply GID + `NotifyGameCreated` (0x0F) + `NotifyGameSetup` (0x14) + `NotifyPlayerJoining` (0x15)
3. Client → `finalizeGameCreation` (0x0F) — server replies + fires `NotifyGameStateChange` + `NotifyGamePlayerStateChange` + `NotifyPlayerJoinCompleted`
4. Client → `updateMeshConnection` (0x1D) — server fires `NotifyGamePlayerStateChange` + `NotifyPlayerJoinCompleted`

> C++ WORKING log: GID=2, player STAT=ACTIVE_CONNECTED(4), PHST.HPID=2, PHST.HSLT=**1** (topology host HSLT=0). `[V]` `:20-96`
> C++ BAD log: GID=1, PHST.HSLT=1 — identical to GOOD but with player IP filled (loopback vs real). `[V]` `:100-177`
> In both logs the GOOD vs BAD distinction correlates with GID (2 vs 1) and player PNET IP (0 vs filled). The comment marks BAD but fields are near-identical; exact difference unclear without full context. `[?]`

---

## Request/response commands

| Command | Cmd ID | C++ handler (file:line) | C# handler (file:line) | Status |
|---|---|---|---|---|
| createGame | 0x01 | `GameManagerComponent.cpp:970` | — | ❌ |
| destroyGame | 0x02 | `GameManagerComponent.cpp:984` | — | ❌ |
| advanceGameState | 0x03 | enum only, not dispatched | — | ❌ |
| setGameSettings | 0x04 | enum only, not dispatched | — | ❌ |
| setPlayerCapacity | 0x05 | enum only, not dispatched | — | ❌ |
| setGameAttributes | 0x07 | enum only, not dispatched | — | ❌ |
| setPlayerAttributes | 0x08 | `GameManagerComponent.cpp:997` | — | ❌ |
| joinGame | 0x09 | `GameManagerComponent.cpp:1002` | — | ❌ |
| removePlayer | 0x0B | `GameManagerComponent.cpp:1044` | — | ❌ |
| startMatchmaking | 0x0D | `GameManagerComponent.cpp:1066` | — | ❌ |
| cancelMatchmaking | 0x0E | `GameManagerComponent.cpp:1075` | — | ❌ |
| finalizeGameCreation | 0x0F | `GameManagerComponent.cpp:1087` | `GameManagerComponent.cs:40` | ✅ |
| listGames | 0x11 | enum only, not dispatched | — | ❌ |
| migrateGame | 0x17 | enum only, not dispatched | — | ❌ |
| resetDedicatedServer | 0x19 | `GameManagerComponent.cpp:1115` | `GameManagerComponent.cs:71` | ✅ |
| banPlayer | 0x1B | enum only, not dispatched | — | ❌ |
| updateMeshConnection | 0x1D | `GameManagerComponent.cpp:1246` | `GameManagerComponent.cs:212` | ✅ |
| updateGameName | 0x27 | enum only, not dispatched | — | ❌ |
| getGameListSnapshot | 0x64 | enum only, not dispatched | — | ❌ |
| getGameListSubscription | 0x65 | enum only, not dispatched | — | ❌ |
| getFullGameData | 0x67 | enum only, not dispatched | — | ❌ |
| getMatchmakingConfig | 0x68 | enum only, not dispatched | — | ❌ |

C++ `ParsePacket` dispatches only: createGame, destroyGame, setPlayerAttributes, joinGame,
removePlayer, startMatchmaking, cancelMatchmaking, finalizeGameCreation, resetDedicatedServer,
updateMeshConnection (10 handlers). `[V]` `GameManagerComponent.cpp:580-627`

C# dispatches only: finalizeGameCreation (0x0F), resetDedicatedServer (0x19),
updateMeshConnection (0x1D) — **3/10**. `[V]` `GameManagerComponent.cs:22-37`

---

## What each handler does (internals)

### resetDedicatedServer (0x19) `[V]`

- **C++** (`cpp:1115`): reads `CreateGameRequest`; calls `GameManager::CreateGame()`; sets
  `user->set_game(game)`; copies all request fields into `gameInfo`; hardcodes
  `gpvh=0xABABABAB`, `gsid=0xCDCDCDCD`, `hses=13666`, `psas="ams"`, `seed=0xFAFAFAFA`,
  `uuid="71bc4bdb-..."`, `state=Initializing`; sets `networkQos{dbps=128000,type=Open,ubps=2}`;
  `pHost={userId,0}`, `tHost={userId,0}`; builds one `ReplicatedGamePlayer`
  (slot=userSlot=0, state=`Connecting`, tIndex from request); reads `SelectedDifficulty` attr
  → `chainData.SetLevelByIndex(levelId)` + `SetStarLevel(0)` + `SetCompleted(false)` +
  `game->LoadLevel()`; replies `WriteJoinGame(GID)`; fires `NotifyGameCreated` →
  `NotifyGameSetup` → `NotifyPlayerJoining`. `cpp:1115-1243`
- **C#** (`cs:71`): reads `CreateGameRequest`; `GameHandler?.CreateGame()`; sets
  `game.SelectLevel(levelIndex)` from `SelectedDifficulty`; calls `game.SetupPlayer` + 9×
  `game.SetupBot`; replies `JoinGameResponse{GID, JoinState.JoinedGame}`; builds
  `NotifyGameSetup` (populates all `ReplicatedGameData` fields, hardcodes same magic constants);
  adds 5 extra `GameAttribs` keys (`ServerBuildVersion`, `GameOwnerId`, `GameOwnerName`,
  `GameFlags`, `ExpectedPlayerCount`, `PrivateMatch`); fires `NotifyGameCreated` →
  `NotifyGameSetup` → `NotifyPlayerJoining`. `cs:71-209`
- **Divergence**: C++ player `state=Connecting(2)`; C# player `state=ActiveConnecting(2)` — same
  enum value, same wire byte. `[V]` C++ player `slot=userSlot=0`; C# player `SlotId=0`. Match. `[V]`
- **Divergence**: C++ `HNET` in `ReplicatedGameData` comes from `gameData.hostNetwork` (client-
  reported). C# hardcodes `127.0.0.1:42000` for both `hostAddr` (HNET) and player PNET. `[V]`
- **Divergence**: C++ `JoinGameResponse` = `WriteJoinGame` → single `GID` field only. C# replies
  `JoinGameResponse{GID, JGS=JoinedGame}` — adds `JGS`. `[?]` whether client reads/ignores `JGS`.
- **Divergence**: C++ `seed=0xFAFAFAFA`; C# `SharedSeed=0xFAFAFAFA`. Match. `[V]`
- **Divergence**: C++ `PHST.HSLT=userSlot=0`, `THST.HSLT=userSlot=0`. C# `PlatformHostInfo.SlotId=0`,
  `TopologyHostInfo.SlotId=0`. Match on slot=0. BUT WORKING log shows `PHST.HSLT=1` (`:53`),
  suggesting C++ may set it 1 in a different code path or the log is from the secondary PHST
  struct in the `#else` block. `[?]`

### finalizeGameCreation (0x0F) `[V]`

- **C++** (`cpp:1087`): reads GID from request (unused; uses `user->get_game()`);
  `GameManager::StartGame(gameId)`; replies `WriteJoinGame(GID)`; fires
  `NotifyGameStateChange(InGame)` → `NotifyGamePlayerStateChange(Connected)` →
  `NotifyPlayerJoinCompleted`. `cpp:1087-1113`
- **C#** (`cs:40`): reads `UpdateGameSessionRequest` (XNNC/XSES blobs); replies empty
  `RespondTo`; hardcodes `gameId=1`; fires `NotifyGameStateChange(InGame)` →
  `NotifyGamePlayerStateChange(ActiveConnected)` → `NotifyPlayerJoinCompleted`. `cs:40-68`
- **Divergence**: C++ reads GID from request then uses `user->get_game()->GetId()`. C# ignores
  request GID entirely; hardcodes `gameId=1`. `[V]`
- **Divergence**: C++ replies `WriteJoinGame(GID)` (GID field). C# replies empty packet. `[V]`
- **Divergence**: C++ reads `CreateGameRequest`-shaped body from FinalizeGameCreation. C#
  reads `UpdateGameSessionRequest` (GID+XNNC+XSES). These are different TDF shapes — the C#
  request struct likely mismatches. `[?]` confirm client sends for finalizeGameCreation.

### updateMeshConnection (0x1D) `[V]`

- **C++** (`cpp:1246`): reads `TARG` list of `PlayerConnectionStatus`; reads GID; loops targets;
  branch on `playerState == Connecting`: if GameplayUser fires
  `NotifyGamePlayerStateChange(Connected)` + `NotifyPlayerJoinCompleted` using
  `user->get_id()`; if DedicatedServer uses `personaId` from first target. Branch on
  `playerState == Reserved`: fires `NotifyPlayerRemoved(PlayerConnLost)` either way. `cpp:1246-1288`
- **C#** (`cs:212`): reads `UpdateMeshConnectionRequest`; loops `request.Target`; branch on
  `target.PlayerConnectionState == Connected` fires `NotifyGamePlayerStateChange` +
  `NotifyPlayerJoinCompleted` using `target.PlayerId`. No Reserved/disconnect branch. `cs:212-238`
- **Divergence**: C# does not handle `PlayerState.Reserved` → no `NotifyPlayerRemoved` on
  disconnect. `[V]`
- **Divergence**: C++ has GameplayUser vs DedicatedServer branching on client type; C# does not
  distinguish — always uses `target.PlayerId`. `[V]`

### createGame (0x01) `[V]` — C++ only

- C++ (`cpp:970`): reads `CreateGameRequest`; replies `WriteCreateGame(GID=1)`;
  fires `NotifyGameStateChange(Initializing)`. Does NOT fire `NotifyGameSetup`. `[V]`
  (`NotifyGameSetup` call is commented out at `:981`.) `[?]` whether client uses createGame at
  all in this flow or only resetDedicatedServer.

### joinGame (0x09) `[V]` — C++ only

- C++ (`cpp:1002`): resolves game by GID; `user->set_game(game)`; builds
  `ReplicatedGamePlayer` (slot=1, state=Connected); replies `WriteJoinGame(GID)`;
  fires `NotifyPlayerJoining`. `cpp:1002-1042`

### removePlayer (0x0B) `[V]` — C++ only

- C++ (`cpp:1044`): reads GID/PID/REAS; `game->RemovePlayer(personaId)`; `user->set_game(nullptr)`;
  replies empty; fires `NotifyPlayerRemoved`. `cpp:1044-1064`

---

## Key TDF field tables

### ReplicatedGameData (GAME struct in NotifyGameSetup) `[V]` `Functions.cpp:614-708`

| Tag | Type | C++ value | C# value | Notes |
|---|---|---|---|---|
| ADMN | list&lt;i64&gt; | administrators list | `AdminPlayerList` | C# adds `client.UserId` if absent `cs:173` |
| ATTR | map&lt;str,str&gt; | attributes (from request) | `GameAttribs` | C# appends 5+ extra keys `cs:154-169` |
| CAP  | list&lt;u16&gt; | capacity list | `SlotCapacities` | from request |
| CRIT | map&lt;str,str&gt; | criteria | `EntryCriteriaMap` | |
| GID  | u64 | game id | `GameId` | assigned by GameManager |
| GNAM | string | name from request | `GameName` | |
| GPVH | u64 | **0xABABABAB** | **0xABABABAB** | hardcoded both `[V]` |
| GSET | u32 | settings from request (260=0x104) | `GameSettings` | |
| GSID | u64 | **0xCDCDCDCD** | **0xCDCDCDCD** | hardcoded both `[V]` |
| GSTA | enum | Initializing(1) | Initializing(1) | `[V]` |
| GTYP | string | type from request | `GameTypeName` | |
| HNET | list&lt;struct&gt; | hostNetwork from request (EXIP+INIP) | hardcoded 127.0.0.1:42000 | ⚠️ divergence |
| HSES | u64 | **13666** | **13666** | hardcoded both `[V]` |
| IGNO | bool | from request | `IgnoreEntryCriteriaWithInvite` | |
| MCAP | u16 | maxPlayers | `MaxPlayerCapacity` | |
| NQOS | struct | dbps=128000, NATT=Open, ubps=2 | same | `[V]` |
| NRES | bool | `resetable ? 0 : 1` | `ServerNotResetable` | NOTE: C++ inverts — `true` = NOT resetable; C# field name matches semantics `[V]` |
| NTOP | enum | CLIENT_SERVER_DEDICATED(1) | `NetworkTopology` | from request |
| PGID | string | playgroupId | `PlaygroupId` | |
| PGSR | blob | empty | `PlaygroupIdSecret` (blob) | |
| PHST | struct | `pHost{userId, 0}` | `PlatformHostInfo{UserId, 0}` | WORKING log shows HSLT=1 `[?]` |
| PRES | enum | from request | `PresenceMode` | |
| PSAS | string | **"ams"** | **"ams"** | hardcoded both `[V]` |
| QCAP | u16 | queueCapacity | `QueueCapacity` | |
| SEED | u32 | **0xFAFAFAFA** | **0xFAFAFAFA** | hardcoded both `[V]` |
| TCAP | u16 | tcap | `TeamCapacity` | |
| THST | struct | `tHost{userId, 0}` | `TopologyHostInfo{UserId, 0}` | `[V]` |
| TIDS | list&lt;u16&gt; | `[userSlot=0]` | `TeamIds` (from request) | C++ always pushes `[0]`; C# uses request value |
| UUID | string | "71bc4bdb-82ec-494d-8d75-ca5123b827ac" | same | hardcoded both `[V]` |
| VOIP | enum | Disabled(0) | `VoipNetwork` | C++ hardcodes Disabled; C# uses request value |
| VSTR | string | version from request | `VersionString` | |
| XNNC | blob | empty | `XnetNonce` (blob) | |
| XSES | blob | empty | `XnetSession` (blob) | |

> C++ `ReplicatedGameData::Write` does NOT emit: `GURL`, `MATR` (commented out `:668`). C# struct
> has both fields and will emit them (empty strings/maps) if the TDF serializer emits defaults.
> `[V]` `cpp:668-671` vs `cs:ReplicatedGameData`

> C++ WORKING log ALSO shows missing field: no `PRES` visible in the truncated dump. Wire log
> is partial so no definitive conclusion. `[?]`

### ReplicatedGamePlayer (PROS list entry) `[V]` `Functions.cpp:579-610`

| Tag | Type | C++ value | C# value |
|---|---|---|---|
| BLOB | blob | empty | `CustomData` (empty) |
| EXID | u64 | 0 | 0 |
| GID  | u64 | gameId | `GameId` |
| LOC  | u32 | `client.data().lang` | `0x656E5553` (enUS hardcoded) |
| NAME | string | `user->get_name()` | `client.Username` |
| PATT | map&lt;str,str&gt; | empty | empty |
| PID  | u64 | user id | `PlayerId = client.UserId` |
| PNET | union(IpPairAddress) | hostNetwork (EXIP+INIP from request) | 127.0.0.1:42000 both `[V]` |
| SID  | u8 | slot (=userSlot=0) | 0 |
| SLOT | enum | Public(0) | Public(0) |
| STAT | enum | **Connecting(2)** | **ActiveConnecting(2)** — same wire value `[V]` |
| TIDX | u16 | tIndex (from request `TIDX`) | 0xFFFF hardcoded |
| TIME | u32 | `utils::get_unix_time()` | `CurrentUnixTime` |
| UGID | objectId | 0/0/0 | `BlazeObjectId` (0/0/0) |
| UID  | u64 | uid (= userId in single-player) | `PlayerSessionId = client.UserId` |

> `TIDX` divergence: C++ copies `tIndex` from the request `TIDX` field; C# hardcodes 0xFFFF.
> The WORKING log shows `TIDX = 65535 (0xFFFF)` — so for this session the request `TIDX`
> happened to be 0xFFFF anyway. `[V]` consistent. `[?]` confirm no other value is ever sent.

### NotifyGameCreated (0x0F) `[V]` `cpp:680`

| Tag | Type | C++ | C# |
|---|---|---|---|
| GID | u64 | gameId | `GameId` |

### NotifyGameSetup (0x14) top-level fields `[V]` `cpp:695`

| Tag | Type | Contents |
|---|---|---|
| GAME | struct | `ReplicatedGameData` (see table above) |
| PROS | list&lt;struct&gt; | `ReplicatedGamePlayer` per player |
| REAS | union | `GameSetupReason` — `DCTX=CreateGame(0)` in `VALU` struct |

> C++ REAS union discriminant = `NetworkAddressMember::XboxClientAddress` (numeric value used as
> union tag). C# uses `GameSetupReasonMember.DatalessSetupContext = 0`. These are different enum
> types used as union discriminants — wire encoding depends on whether the underlying values
> match. `[?]` confirm C++ `NetworkAddressMember::XboxClientAddress` == 0 or check wire log.

> C# `NotifyGameSetup` also has `QUEU` field (queue roster, empty); C++ writes no QUEU. `[V]`
> `cs:NotifyGameSetup.GameQueue` vs no `QUEU` in `cpp:709-729`.

### NotifyGameStateChange (0x64) `[V]` `cpp:906`

| Tag | Type | C++ | C# |
|---|---|---|---|
| GID  | u64 | gameId | `GameId` |
| GSTA | enum | GameState | `GameState` |

### NotifyGamePlayerStateChange (0x74) `[V]` `cpp:937`

| Tag | Type | C++ | C# |
|---|---|---|---|
| GID  | u64 | gameId | `GameId` |
| PID  | u64 | personaId | `PlayerId` |
| STAT | enum | PlayerState | `PlayerState` |

### NotifyPlayerJoining (0x15) `[V]` `cpp:864`

| Tag | Type | Contents |
|---|---|---|
| GID  | u64 | gameId |
| PDAT | struct | `ReplicatedGamePlayer` (same layout as PROS entry) |

### NotifyPlayerJoinCompleted (0x1E) `[V]` `cpp:880`

| Tag | Type | C++ | C# |
|---|---|---|---|
| GID | u64 | gameId | `GameId` |
| PID | u64 | personaId | `PlayerId` |

### NotifyPlayerRemoved (0x28) `[V]` `cpp:888`

| Tag | Type | C++ | C# |
|---|---|---|---|
| CNTX | u16 | 0 | `TitleContext` (0) |
| GID  | u64 | gameId | `GameId` |
| PID  | u64 | personaId | `PlayerId` |
| REAS | enum | PlayerRemovedReason | `Reason` |

---

## Notifications fired table

| Notification | ID | C++ sender | C# sender | Single-player path |
|---|---|---|---|---|
| NotifyMatchmakingFailed | 0x0A | `cancelMatchmaking` `:1084` | — | ❌ |
| NotifyMatchmakingAsyncStatus | 0x0C | (defined, not called in ParsePacket) | — | ❌ |
| NotifyGameCreated | 0x0F | `resetDedicatedServer` `:1236` | `cs:200` | ✅ |
| NotifyGameRemoved | 0x10 | `destroyGame` `:994` | — | ❌ |
| NotifyGameSetup | 0x14 | `resetDedicatedServer` `:1239` | `cs:201` | ✅ |
| NotifyPlayerJoining | 0x15 | `resetDedicatedServer` `:1240-1242`, `joinGame` `:1041` | `cs:203-207` | ✅ |
| NotifyPlayerJoinCompleted | 0x1E | `finalizeGameCreation` `:1112`, `updateMeshConnection` `:1268,1274` | `cs:62-66`, `cs:224-235` | ✅ |
| NotifyPlayerRemoved | 0x28 | `removePlayer` `:1063`, `updateMeshConnection` `:1280,1285` | — | ❌ |
| NotifyPlatformHostInitialized | 0x47 | defined at `:898`, not called in ParsePacket | — | ❌ |
| NotifyGameStateChange | 0x64 | `createGame` `:981`, `finalizeGameCreation` `:1110` | `cs:53` | ✅ |
| NotifyGameReset | 0x70 | defined at `:914`, not called in ParsePacket | — | ❌ |
| NotifyGameSessionUpdated | 0x73 | defined at `:928`, not called in ParsePacket | — | ❌ |
| NotifyGamePlayerStateChange | 0x74 | `finalizeGameCreation` `:1111`, `updateMeshConnection` `:1269,1273` | `cs:55-60`, `cs:224-229` | ✅ |
| NotifyCreateDynamicDedicatedServerGame | 0xDC | defined at `:946`, not called in ParsePacket | — | ❌ |

---

## Divergences (C++ vs C#)

1. **HNET / player PNET address source.** C++ copies `hostNetwork` from the client-reported
   `CreateGameRequest.HNET` (EXIP+INIP pair). C# hardcodes both to `127.0.0.1:42000`. For
   loopback single-player this is functionally equivalent, but the wire content differs from
   what C++ sends. `[V]` `cpp:1163` vs `cs:105-113,190-193`

2. **Extra GameAttribs in C#.** C# appends `ServerBuildVersion`, `GameOwnerId`, `GameOwnerName`,
   `GameFlags`, `ExpectedPlayerCount`, `PrivateMatch` to `ATTR`. C++ sends only the attrs from
   the request. These extra keys may affect how the client GFx scripts bind game info. `[V]`
   `cs:154-169`

3. **finalizeGameCreation reply.** C++ replies `WriteJoinGame(GID)` (GID field). C# replies
   empty packet (`RespondTo` with no body). `[V]` `cpp:1104-1107` vs `cs:44`

4. **finalizeGameCreation gameId.** C# hardcodes `gameId=1` for all notifications; C++ uses
   `game->GetId()`. If a second game is created with a different id, C# sends wrong GID in
   all three finalizeGameCreation notifications. `[V]` `cs:51`

5. **finalizeGameCreation request struct mismatch.** C++ reads a `CreateGameRequest`-shaped body.
   C# reads `UpdateGameSessionRequest` (GID+XNNC+XSES). `[?]` verify what the client actually
   sends for cmd 0x0F.

6. **QUEU field in NotifyGameSetup.** C# `NotifyGameSetup` includes a `QUEU` list; C++ does not
   write QUEU. If the TDF serializer emits empty lists by default, C# sends an extra field. `[V]`
   `cs:NotifyGameSetup` vs `cpp:695-729`

7. **ReplicatedGameData missing fields in C++.** C++ `Write` omits `GURL` and `MATR` (MATR is
   commented out). C# struct declares both and may emit them. If client requires exact field set
   this is a potential malformed-packet issue. `[V]` `cpp:645,668-671`

8. **updateMeshConnection: no disconnect handling.** C# never fires `NotifyPlayerRemoved` for
   Reserved/disconnecting state. C++ fires it in both GameplayUser and DedicatedServer branches.
   `[V]` `cpp:1276-1287` vs `cs:212-238`

9. **REAS union discriminant type mismatch.** C++ uses `NetworkAddressMember::XboxClientAddress`
   as the union tag for REAS; C# uses `GameSetupReasonMember.DatalessSetupContext = 0`. The
   numeric value may differ — if `XboxClientAddress != 0` the union type byte on wire disagrees
   and the client will misparse `REAS`. `[?]` confirm value of `NetworkAddressMember::XboxClientAddress`.

10. **Player TIDX.** C++ copies `tIndex` from request `TIDX`; C# hardcodes `0xFFFF`. Consistent
    with WORKING log (which shows `TIDX=0xFFFF`), but worth confirming client always sends `TIDX=0xFFFF`.
    `[V]` wire log matches C# constant.

11. **LOC in player.** C++ uses `client.data().lang` (client-reported locale). C# hardcodes
    `0x656E5553` (enUS). `[V]` `cpp:1205` vs `cs:181`

12. **Missing handlers (7 C++ → 0 C#).** createGame, destroyGame, setPlayerAttributes, joinGame,
    removePlayer, startMatchmaking, cancelMatchmaking all have C++ handlers with no C# equivalent.
    These are multiplayer paths; single-player currently routes only via resetDedicatedServer. `[V]`

---

## Open questions / Ghidra TODO

- `[?]` Confirm `NetworkAddressMember::XboxClientAddress` numeric value (divergence #9). If != 0
  the REAS union type byte is wrong in C# and the client may throw a parse error on
  `NotifyGameSetup`. This is a candidate for the Dungeon-entry crash.
- `[?]` Does the client require `GURL` / `MATR` in `ReplicatedGameData`, or are missing fields
  tolerated? C++ omits them; C# emits them (empty). Check Ghidra `cReplicatedGameData` parser.
- `[?]` Does the client read `QUEU` in `NotifyGameSetup`? C# emits it; C++ doesn't. Potential
  parse issue if the client does not expect it.
- `[?]` Confirm what TDF body the client sends for `finalizeGameCreation` (0x0F) — is it a
  `CreateGameRequest` shape (C++ expects) or an `UpdateGameSession` shape (C# reads)?
- `[?]` Confirm PHST.HSLT wire value. WORKING log shows HSLT=1 for PHST, but C++ code sets
  `pHost.slot = userSlot = 0`. Possible the log shows a different PHST struct (the `#else`
  block at `:799` set HSLT=1).
- `[?]` Does the client ever send `createGame` (0x01) vs always sending `resetDedicatedServer`
  (0x19)? Determines whether the missing createGame handler matters.
- `[?]` Verify `GameState` enum values: C++ `InGame` is dispatched from `finalizeGameCreation`;
  C# uses `GameState.InGame = 0x83`. Confirm C++ enum value matches `[?]`.
