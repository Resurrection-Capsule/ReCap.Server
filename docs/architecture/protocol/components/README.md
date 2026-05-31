# Blaze Component Index

One sheet per Blaze component. "Coverage" = commands handled in C# / commands dispatched in C++ (request commands only; notifications counted separately). **All 13 sheets carry a full `Audit pass 2026-05-29` verification** (C++ source + C# port, file:line cited, `[V]`/`[?]` tagged); Ghidra was not reachable this pass.

| Component | ID (hex) | C# file | Coverage | Status summary |
|---|---|---|---|---|
| [Authentication](auth.md) | 0x0001 | `AuthenticationComponent.cs` | 10/10 | All login-path commands implemented; three C#-only extras (0xF1/F2/F6). |
| [GameManager](gamemanager.md) | 0x0004 | `GameManager/GameManagerComponent.cs` | 3/10 | Critical path (resetDedicatedServer, finalizeGameCreation, updateMeshConnection) done. **NotifyGameSetup field divergences (REAS union tag, extra QUEU/GURL/MATR) = top Dungeon-crash suspect.** Matchmaking/joinGame/removePlayer absent. |
| [Redirector](redirector.md) | 0x0005 | `RedirectorComponent.cs` | 1/1 | Complete. Minor divergences: union member tag (1 vs 0), SECU (1 vs false), port sourced statically. |
| [Playgroups](playgroups.md) | 0x0006 | `PlaygroupsComponent.cs` | 1/11 | Only `createPlaygroup` stubbed (echo, no persistence); all 11 notifications absent. C++ handlers also empty stubs. Sufficient for solo flow. |
| [Util](util.md) | 0x0009 | `UtilComponent.cs` | 3/18 | ping, preAuth, postAuth done. Divergences: CIDS list (18 vs 9), CONF.pingPeriod format, TELE.ANON dropped, ClientData not persisted. getTelemetryServer/userSettings*/setClientMetrics absent. |
| [CensusData](censusdata.md) | 0x000A | — (no C# port) | 0/3 | Entirely absent in C#; in C++ also no-op stubs AND not registered in `ComponentManager::Get` (dropped before dispatch). Not on solo path. |
| [Messaging](messaging.md) | 0x000F | `MessagingComponent.cs` | 2/5 | fetchMessages + purgeMessages reply `{MCNT=0}` (correct — C++ stubs send nothing). sendMessage absent (no chat relay). `ServerMessage.Encode` throws NotImplemented (latent). |
| [Rooms](rooms.md) | 0x0015 | `RoomsComponent.cs` | 3/22 | **joinRoom (0x14) now handled + RoomManager pre-seeds views/categories/rooms (2026-05-30)**; selectView/selectCategory ported faithful. Awaiting client test. 12/16 notifications still missing. |
| [AssociationLists](association.md) | 0x0019 | `AssociationListsComponent.cs` | 1/8 | getLists done (hardcoded ignore list ID=101); addUsersToList/removeUsersFromList absent; spurious PresenceInfo fields; BLID truncated u32; no NotifyUpdateListMembership. |
| [UserSessions](usersession.md) | 0x7802 | `UserSessionsComponent.cs` | 4/12 | lookupUser, updateNetworkInfo, updateUserSessionClientData, setUserInfoAttribute done; fetchExtendedData, lookupUsers, geo-IP commands absent. |
| [GameReporting](gamereporting.md) | 0x001C | `GameManager/GameReportingComponent.cs` | 0/15 | C#-only skeleton (15 cmds name-only, all return false). No C++ ref — listed `// unused` and excluded from `ComponentManager::Get`. Not on solo path. |
| [UnknownComponent1](unknowncomponent1.md) | 0x2678 | `UnknownComponent1.cs` | 1/1 | C#-only no-op at command 0x200; zero matches in C++ tree. Likely title-specific SDK component; latent risk only if 0x200 needs a reply. |

## Coverage notes

- **Authentication** and **Redirector** are functionally complete for the offline single-player login flow.
- **GameManager** covers the three commands the client actually uses to start a game session; the rest are multiplayer-only paths.
- **Playgroups**, **Rooms**, and **AssociationLists** have minimal stubs that satisfy the client enough to proceed past the lobby screens but lack real game logic.
- **CensusData** is entirely absent in C# and is not needed for offline play.
- **GameReporting** and **UnknownComponent1** are C#-only additions with no reference counterpart.
- Overall C# coverage of the C++ handler surface: approximately 65% of commands on the critical login→game path, ~25% of total defined commands across all components.

## Crash-lead leads surfaced by the audit

Two component sheets carry the strongest leads for the known Dungeon-entry client crash:

1. **Rooms `joinRoom` (0x14) is dropped** — the verified C++ log shows `joinRoom` as the last Rooms exchange before `resetDedicatedServer`; C# has no case for it, so the client's blocking request gets no reply. C++ pre-seeds view 1 / categories 1-4 / rooms 1-4 in `RoomManager()`; C# does not, so even adding a handler needs the pre-seed or it hits null-category lookup. See [rooms.md](rooms.md).
2. **GameManager `NotifyGameSetup` field divergences** — REAS union discriminant (`XboxClientAddress` vs `DatalessSetupContext=0`), an extra `QUEU` field C++ never emits, and `GURL`/`MATR` present in C# but omitted by C++ `ReplicatedGameData::Write`. Any of these can desync TDF parsing → misparse → crash at the `cPlayerDeck` GFx bind. See [gamemanager.md](gamemanager.md).

Both are `[?]` pending Ghidra confirmation of the exact client-side union/field expectations.
