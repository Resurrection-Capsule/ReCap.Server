# GameReporting Component — 0x1C

End-of-match telemetry component. C#-only skeleton: no `GameReportingComponent.cpp` exists under
`ReCap.Cpp/darkspore_server/source/Blaze/Component/`. The C++ reference names the component in an
**"unused components"** enum and defines its error-code space, but deliberately never wires it into
the dispatcher. All C++ cells are `— (no reference)` throughout. Not on the solo login path;
**zero** GameReporting messages appear in a complete WORKING C++ runtime log.

Audit pass 2026-05-29: verified against C++ `Blaze/Component.cpp` (enum + dispatcher),
`Blaze/Types.h` (error codes), C# port
`ReCap.Server/Adapters/Blaze/Component/GameManager/GameReportingComponent.cs`, and
`ReCap.Server/Adapters/Blaze/Tdf.cs` (type-hash registry). Tags: `[V]` = verified in cited
source, `[?]` = unverified / needs Ghidra or packet capture.

---

## C++ tree status `[V]`

| Finding | File:line |
|---|---|
| `GameReporting = 0x1C` listed under `// unused components` enum | `Component.cpp:15-20` |
| `ComponentManager::Get` switch covers Auth/GameManager/Redirector/Playgroups/Util/Messaging/Rooms/Association/UserSession only; default `return nullptr` — 0x1C **not dispatched** | `Component.cpp:45-94` |
| No `GameReportingComponent.h` / `.cpp` include or file in `Component/` subdirectory | confirmed by directory listing |
| Error-code family `GAMEREPORTING_ERR_*` / `GAMEREPORTING_OFFLINE_ERR_*` / `GAMEREPORTING_TRUSTED_ERR_*` defined (confirms real Blaze protocol scope) | `Types.h:584-609` |

---

## Request/response commands

| Command | Cmd ID | C++ handler | C# handler (file:line) | Status |
|---|---|---|---|---|
| submitGameReport | 0x01 | — (no ref) | `GameReportingComponent.cs:24` (name only) | ❌ not handled |
| submitOfflineGameReport | 0x02 | — (no ref) | `GameReportingComponent.cs:25` (name only) | ❌ not handled |
| submitGameEvents | 0x03 | — (no ref) | `GameReportingComponent.cs:26` (name only) | ❌ not handled |
| getGameReportQuery | 0x04 | — (no ref) | `GameReportingComponent.cs:27` (name only) | ❌ not handled |
| getGameReportQueriesList | 0x05 | — (no ref) | `GameReportingComponent.cs:28` (name only) | ❌ not handled |
| getGameReports | 0x06 | — (no ref) | `GameReportingComponent.cs:29` (name only) | ❌ not handled |
| getGameReportView | 0x07 | — (no ref) | `GameReportingComponent.cs:30` (name only) | ❌ not handled |
| getGameReportViewInfo | 0x08 | — (no ref) | `GameReportingComponent.cs:31` (name only) | ❌ not handled |
| getGameReportViewInfoList | 0x09 | — (no ref) | `GameReportingComponent.cs:32` (name only) | ❌ not handled |
| getGameReportTypes | 0x0A | — (no ref) | `GameReportingComponent.cs:33` (name only) | ❌ not handled |
| updateMetric | 0x0B | — (no ref) | `GameReportingComponent.cs:34` (name only) | ❌ not handled |
| getGameReportColumnInfo | 0x0C | — (no ref) | `GameReportingComponent.cs:35` (name only) | ❌ not handled |
| getGameReportColumnValues | 0x0D | — (no ref) | `GameReportingComponent.cs:36` (name only) | ❌ not handled |
| submitTrustedMidGameReport | 0x64 | — (no ref) | `GameReportingComponent.cs:37` (name only) | ❌ not handled |
| submitTrustedEndGameReport | 0x65 | — (no ref) | `GameReportingComponent.cs:38` (name only) | ❌ not handled |

`[V]` `HandlePacket` contains no `case` labels — every inbound command falls to `default`, logs
`[Game Reporting component]: Unknown command: <id>`, returns `false`.
`GameReportingComponent.cs:10-18`

---

## Handler internals (C# only)

### HandlePacket (all commands) `[V]` `GameReportingComponent.cs:10-18`

```
switch (packet.Command) {
    default:
        Log($"Unknown command: {packet.Command}");
        return false;
}
```

No reply is sent for any command ID. Any client message addressed to 0x1C is silently dropped
after logging. `[V]`

### GetCommandName `[V]` `GameReportingComponent.cs:21-41`
Maps the 15 command IDs above to their canonical Blaze SDK names. Unknown IDs return `"<unknown>"`.
Used by the Blaze logging layer; has no protocol effect.

### GetNotificationName `[V]` `GameReportingComponent.cs:43-49`
Maps notification 0x72 to `"ResultNotification"`. Not sent anywhere in C#.

### Registration `[V]`
Instantiated and registered in `BlazeServer.cs:70`; `AttachComponent` inserts it at
`Components[0x1C]` (`BlazeServer.cs:101`). Presence in the component map means the server will
route any 0x1C-addressed Blaze packet to `HandlePacket` rather than returning an
"unknown component" error — but `HandlePacket` immediately returns `false` for every command.

---

## TDF field tables

No replies are implemented. No TDF structs are constructed or sent by this component.

The C# TDF type-hash decoder (`Tdf.cs`) recognises **many** `Blaze::GameReporting::*` struct
hashes from the standard Blaze SDK (e.g. `0x1CCC9EAB` = `IntegratedSample::Report`,
`0x0A26A2E9` = `SampleBase::Report`, and ~40 more). These are present solely so the wire decoder
can label inbound TDF blobs; none are constructed for replies. `[V]` `Tdf.cs:381-476`

---

## Notifications fired

None. `GetNotificationName(0x72)` maps `ResultNotification` by name only; it is never called to
actually enqueue a notification. `[V]` `GameReportingComponent.cs:43-49`

---

## Solo-path relevance `[V]`

A complete WORKING C++ runtime log (`output.txt`, full session: login → lobby → dungeon →
gameplay → logout) contains **zero** `GameReporting` / `0x1C` lines. Post-gameplay teardown is
`GameManager::removePlayer` + REST `api.account.logout` — no game report is submitted for a
session that quits mid-dungeon (`removePlayer REAS=6`). Component has **no impact** on solo login
or dungeon-entry flow. Deprioritize entirely for dungeon-entry crash investigation.

---

## Divergences

C#-only addition, no reference behavior to verify against. The C++ reference deliberately
marks this component unused and provides no handler. All 15 command names in C# are canonical
Blaze SDK RPCs — their correct request/reply TDF layout is unknown for Darkspore without a
real match-completion packet capture or Ghidra RE of the client's 0x1C dispatch. `[V]`

---

## Open questions / Ghidra TODO

- `[?]` Does the Darkspore client ever send `submitOfflineGameReport` (0x02) on normal level
  completion (vs. quit)? The captured session quit mid-dungeon; re-capture a finished match.
- `[?]` If the client sends 0x1C and gets `false` back (no reply), does it stall, retry, or
  silently continue? Current solo play works with no reply; confirm this is not a latent issue
  in a true match-completion path.
- `[?]` Full TDF layout of `submitOfflineGameReport` request + expected reply. C# knows type
  hash `0x1CCC9EAB` (`IntegratedSample::Report`, `Tdf.cs:388`) but not the field schema.
  Needs client RE or a Wireshark capture of a real match end.
- `[?]` Ghidra: search `submitOfflineGameReport` string + `0x1C` component dispatch in the
  retail client to recover the report struct and confirm whether a reply is required.
