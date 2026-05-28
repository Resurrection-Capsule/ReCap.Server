# Blaze Component Index

One sheet per Blaze component. "Coverage" = commands handled in C# / commands dispatched in C++ (request commands only; notifications counted separately).

| Component | ID (hex) | C# file | Coverage | Status summary |
|---|---|---|---|---|
| [Authentication](auth.md) | 0x0001 | `AuthenticationComponent.cs` | 10/10 | All login-path commands implemented; three C#-only extras (0xF1/F2/F6). |
| [GameManager](gamemanager.md) | 0x0004 | `GameManager/GameManagerComponent.cs` | 3/10 | Critical path (resetDedicatedServer, finalizeGameCreation, updateMeshConnection) done; matchmaking, joinGame, removePlayer absent. |
| [Redirector](redirector.md) | 0x0005 | `RedirectorComponent.cs` | 1/1 | Complete — single command fully implemented. |
| [Playgroups](playgroups.md) | 0x0006 | `PlaygroupsComponent.cs` | 1/11 | Only `createPlaygroup` stubbed; all notifications absent. Sufficient for solo flow. |
| [Util](util.md) | 0x0009 | `UtilComponent.cs` | 3/18 | ping, preAuth, postAuth done; getTelemetryServer, userSettings*, setClientMetrics absent. |
| [CensusData](censusdata.md) | 0x000A | — (C++ only) | 0/3 | Entirely absent in C#. Not on solo login path. |
| [Messaging](messaging.md) | 0x000F | `MessagingComponent.cs` | 2/5 | fetchMessages + purgeMessages (stub acks); sendMessage absent — no chat relay. |
| [Rooms](rooms.md) | 0x0015 | `RoomsComponent.cs` | 2/12 | selectViewUpdates + selectCategoryUpdates (partial); joinRoom absent; 13/15 notifications missing. |
| [AssociationLists](association.md) | 0x0019 | `AssociationListsComponent.cs` | 1/8 | getLists done (hardcoded data); add/remove absent; no NotifyUpdateListMembership. |
| [UserSessions](usersession.md) | 0x7802 | `UserSessionsComponent.cs` | 4/12 | lookupUser, updateNetworkInfo, updateUserSessionClientData, setUserInfoAttribute done; fetchExtendedData, lookupUsers, geo-IP commands absent. |
| [GameReporting](gamereporting.md) | 0x001C | `GameManager/GameReportingComponent.cs` | 0/15 | C#-only skeleton; all commands unimplemented. Not on solo login path. |
| [UnknownComponent1](unknowncomponent1.md) | 0x2678 | `UnknownComponent1.cs` | 1/? | C#-only; purpose unknown; single no-op handler at command 0x200. |

## Coverage notes

- **Authentication** and **Redirector** are functionally complete for the offline single-player login flow.
- **GameManager** covers the three commands the client actually uses to start a game session; the rest are multiplayer-only paths.
- **Playgroups**, **Rooms**, and **AssociationLists** have minimal stubs that satisfy the client enough to proceed past the lobby screens but lack real game logic.
- **CensusData** is entirely absent in C# and is not needed for offline play.
- **GameReporting** and **UnknownComponent1** are C#-only additions with no reference counterpart.
- Overall C# coverage of the C++ handler surface: approximately 65% of commands on the critical login→game path, ~25% of total defined commands across all components.
