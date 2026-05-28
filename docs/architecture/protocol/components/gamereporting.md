# GameReporting Component — 0x1C

Receives end-of-match game reports from the client. C#-only component with no C++ counterpart in the reference implementation.

**C# only.** No corresponding C++ source file exists in the reference tree.

---

## Request/response commands

| Command name | Cmd ID | Direction | C++ handler | C# handler | Status |
|---|---|---|---|---|---|
| submitGameReport | 0x01 | C→S | — | — (name registered, no handler) | ⚠️ |
| submitOfflineGameReport | 0x02 | C→S | — | — (name registered, no handler) | ⚠️ |
| submitGameEvents | 0x03 | C→S | — | — (name registered, no handler) | ⚠️ |
| getGameReportQuery | 0x04 | C→S | — | — (name registered, no handler) | ⚠️ |
| getGameReportQueriesList | 0x05 | C→S | — | — (name registered, no handler) | ⚠️ |
| getGameReports | 0x06 | C→S | — | — (name registered, no handler) | ⚠️ |
| getGameReportView | 0x07 | C→S | — | — (name registered, no handler) | ⚠️ |
| getGameReportViewInfo | 0x08 | C→S | — | — (name registered, no handler) | ⚠️ |
| getGameReportViewInfoList | 0x09 | C→S | — | — (name registered, no handler) | ⚠️ |
| getGameReportTypes | 0x0A | C→S | — | — (name registered, no handler) | ⚠️ |
| updateMetric | 0x0B | C→S | — | — (name registered, no handler) | ⚠️ |
| getGameReportColumnInfo | 0x0C | C→S | — | — (name registered, no handler) | ⚠️ |
| getGameReportColumnValues | 0x0D | C→S | — | — (name registered, no handler) | ⚠️ |
| submitTrustedMidGameReport | 0x64 | C→S | — | — (name registered, no handler) | ⚠️ |
| submitTrustedEndGameReport | 0x65 | C→S | — | — (name registered, no handler) | ⚠️ |

## Notifications sent (S→C)

| Notification name | Notify ID | Direction | C++ sender | C# sender | Status |
|---|---|---|---|---|---|
| ResultNotification | 0x72 | S→C | — | — (name registered, not sent) | ⚠️ |

---

## Notes

- This component exists in C# (`Adapters/Blaze/Component/GameManager/GameReportingComponent.cs`) as a name registry (`GetCommandName`/`GetNotificationName`) with no `HandlePacket` logic — every command falls to the default/false path.
- The C# `HandlePacket` switch has no cases; all inbound packets from this component ID return `false`.
- The C++ reference has no `GameReportingComponent.*` file; this component was added to C# ahead of any reference behavior being reverse-engineered.
- No TDF field documentation is available. Fields would need to be traced from Ghidra or live traffic capture.

---

## Porting gaps

- All commands are unimplemented. If the client sends any GameReporting packet, the C# server logs an unknown-command warning and returns false (component dispatch failure).
- No porting is needed for the offline single-player flow; game reports are a post-match telemetry concern.
