# CensusData Component — 0x0A

Reports server region population counts to subscribed clients. Entirely absent in the C# implementation.

**C++ source only.** No corresponding C# file exists.

---

## Request/response commands

| Command name | Cmd ID | Direction | C++ handler | C# handler | Status |
|---|---|---|---|---|---|
| subscribeToCensusData | 0x01 | C→S | `Blaze/Component/CensusDataComponent.cpp:49` (stub, commented out) | — | ❌ |
| unsubscribeFromCensusData | 0x02 | C→S | `Blaze/Component/CensusDataComponent.cpp:53` (stub, commented out) | — | ❌ |
| getRegionCounts | 0x03 | C→S | `Blaze/Component/CensusDataComponent.cpp:57` (stub, commented out) | — | ❌ |

## Notifications sent (S→C)

| Notification name | Notify ID | Direction | C++ sender | C# sender | Status |
|---|---|---|---|---|---|
| NotifyServerCensusData | 0x04 | S→C | — (defined in enum, not implemented) | — | ❌ |

---

## Key TDF fields

The C++ implementation stubs all handlers with no body; no TDF structures are read or written. The protocol-level fields are not documented in the C++ source comments.

From the enum ID `NotifyServerCensusData = 0x04`, the expected payload would contain region-keyed player-count data, but the structure is not defined in the reference codebase.

---

## Porting gaps

- The entire component is absent in C#. The C++ `ParsePacket` dispatches all three commands but calls nothing (the call sites are commented out).
- The component ID `0x0A` does not appear in the C# `preAuth` response component list, so the client does not expect this component to be active.
- No action required for the single-player offline scenario; this component is relevant only for live server telemetry / matchmaking UI.
