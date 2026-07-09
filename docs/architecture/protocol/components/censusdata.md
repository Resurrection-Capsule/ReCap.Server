# CensusData Component — 0x0A

Reports server region population counts to subscribed clients. Exists in the C++ reference
as a skeleton with all handler call-sites commented out; the component receives inbound
commands and returns an empty success reply but performs no work. **Entirely absent in the
C# implementation — no port file exists and no C# handler dispatches component 0x0A.**
Not on the solo-player login path; the client never issues any CensusData command during
a single-player session.

Audit pass 2026-05-29: verified against C++ `Blaze/Component/CensusDataComponent.cpp`,
`Blaze/Component/CensusDataComponent.h`, `Blaze/Component.cpp` (dispatch table), and
`Blaze/Component/UtilComponent.cpp` (preAuth CIDS list). Tags: `[V]` = verified in cited
source, `[?]` = unverified / needs Ghidra. Ghidra **not reachable** this pass (no instance).

---

## Solo-path relevance `[V]`

The C++ `ComponentManager::Get` switch (`Component.cpp:46-94`) does **not** include a
`CensusDataComponent` case — the component is listed only in a local `// unused components`
enum (`Component.cpp:16-23`). It is never registered in the live server dispatch table. The
client receives no reply at all if it were to address component 0x0A via the C++ server.

C++ `UtilComponent::PreAuth` CIDS list (`UtilComponent.cpp:185-195`) enumerates 9 component
IDs; **CensusData (0x0A) is absent**. The C# `preAuth` handler advertises component ID `10`
(= 0x0A) in its CIDS list (`UtilComponent.cs:62`). This is a C#-only addition; the client
never calls the component on the solo path regardless. **Not blocking** for offline play.

---

## Request/response commands

| Command | Cmd ID | C++ handler (file:line) | C# handler | Status |
|---|---|---|---|---|
| subscribeToCensusData | 0x01 | `CensusDataComponent.cpp:49` — dispatched, call site commented out | — (absent) | ❌ |
| unsubscribeFromCensusData | 0x02 | `CensusDataComponent.cpp:53` — dispatched, call site commented out | — (absent) | ❌ |
| getRegionCounts | 0x03 | `CensusDataComponent.cpp:57` — dispatched, call site commented out | — (absent) | ❌ |

C++ `ParsePacket` (`CensusDataComponent.cpp:47-66`) dispatches all three command IDs via a
`switch`; each matched case contains only a commented-out function call followed by `break`,
then `ParsePacket` returns `true`. The client therefore receives a generic empty-success Blaze
reply (no TDF body) for any of the three commands. `[V]`

---

## What each handler does (C++ internals) `[V]`

All three handlers are **stub-only** — no implementation exists anywhere in the C++ tree.

### subscribeToCensusData (0x01) `[V]`
- `CensusDataComponent.cpp:49-51`: `case SubscribeToCensusData: // SubscribeToCensusData(request); break;`
- No TDF is read. No subscription is recorded to `mSubscribedClientIds`
  (declared in `CensusDataComponent.h:33` but never written). Returns empty success.

### unsubscribeFromCensusData (0x02) `[V]`
- `CensusDataComponent.cpp:53-55`: `case UnsubscribeFromCensusData: // UnsubscribeFromCensusData(request); break;`
- No TDF is read. `mSubscribedClientIds` not modified. Returns empty success.

### getRegionCounts (0x03) `[V]`
- `CensusDataComponent.cpp:57-59`: `case GetRegionCounts: // GetRegionCounts(request); break;`
- No TDF is read or written. Returns empty success — no region-count payload.

---

## Notifications fired

| Notification | Notify ID | C++ sender | C# sender | Status |
|---|---|---|---|---|
| NotifyServerCensusData | 0x04 | — (defined in enum, call site commented out) | — (absent) | ❌ |

`GetNotificationPacketName` for `NotifyServerCensusData` returns `""` — the case is
commented out (`CensusDataComponent.cpp:40-45`). The notification is never emitted. `[V]`

---

## Key TDF field tables

No TDF structures are read or written anywhere in the C++ implementation — all handler bodies
are empty stubs. `[V]` `CensusDataComponent.cpp:47-66`

### NotifyServerCensusData (0x04) — structure unknown `[?]`
The notification payload structure is not defined in the C++ reference source. Based on the
component purpose (server region population counts), the expected payload would contain
region-keyed player-count data, but no field names, types, or encoding are documented.
Ghidra trace of the live client's `Blaze::CensusData::NotifyServerCensusData` handler would
be required to recover the TDF schema. `[?]`

---

## Dispatch table position `[V]`

`CensusDataComponent` is **not registered** in `ComponentManager::Get`
(`Component.cpp:46-94`). It appears only as a comment-annotated enum value in the local
`// unused components` block (`Component.cpp:16-23`):

```
// unused components
enum ComponentType : uint16_t {
    Stats        = 0x07,
    CensusData   = 0x0A,   // ← listed but never wired
    ...
};
```

This means inbound Blaze packets addressed to component 0x0A are **not dispatched at all**
by the C++ server — `ComponentManager::Get(0x0A)` returns `nullptr`. `[V]` `Component.cpp:45-94`

---

## Divergences (C++ vs C#)

1. **Entire component absent in C#.** No `CensusDataComponent.cs` file exists. The C# server
   has no handler for any of the three commands or the notification. Confirmed by filesystem
   search — zero C# files match `*census*` (case-insensitive). `[V]`
2. **C++ dispatch table excludes CensusData.** `ComponentManager::Get` returns `nullptr` for
   0x0A; inbound packets silently drop. C# would also drop them (no handler), but for a
   different reason (unregistered component vs. absent file). `[V]` `Component.cpp:45-94`
3. **C# preAuth CIDS advertises 0x0A; C++ does not.** C# adds `10` to the CIDS list
   (`UtilComponent.cs:62`). C++ `PreAuth` enumerates 9 IDs, all without CensusData
   (`UtilComponent.cpp:185-195`). Cosmetic difference — client never calls the component. `[V]`
4. **`mSubscribedClientIds` declared but never used.** The header declares an
   `unordered_set<uint32_t>` for tracking subscribers (`CensusDataComponent.h:33`); it is
   never written, read, or passed anywhere. Dead field. `[V]`

---

## Open questions / Ghidra TODO

- `[?]` What TDF schema does the live client expect in `NotifyServerCensusData` (0x04)? Field
  names, types, and encoding are completely absent from the C++ reference. Ghidra trace of
  `Blaze::CensusData` client-side handler required.
- `[?]` Does the live (non-solo) client call `subscribeToCensusData` or `getRegionCounts`
  during matchmaking / server-browser flows? The solo path never triggers it, but a
  multiplayer session might — unknown without a capture or Ghidra session.
- `[?]` Would a real server implementation push `NotifyServerCensusData` proactively on
  subscribe, or only on region-count changes? Push vs. poll model is undefined.
- `[?]` Is the C# CIDS advertisement of 0x0A (`UtilComponent.cs:62`) intentional or a copy
  error vs. the C++ list? Harmless today; remove if it causes unexpected client behaviour.

---

## Client-side (Ghidra strings, 2026-07-08) — what CensusData actually IS

The retail client ships the **full** CensusData class + RPC schema (strings `0x010e64c4`–`0x010e6710`
plus the census-item types `0x010dc4f8`–`0x010dd2e0`), so it *can* parse the feed even though the
solo path never subscribes. Resolves the open questions above (purpose + payload shape):

- **Commands:** `subscribeToCensusData` (0x01), `unsubscribeFromCensusData` (0x02),
  `getRegionCounts` (0x03 → `Blaze::CensusData::RegionCounts`).
- **Notifications:** `NotifyServerCensusData` (0x04, payload field = `mCensusDataList`) +
  `NotifyServerCensusDataItem` (per-item, `Blaze::CensusData::NotifyServerCensusDataItem`).
- **Census-item types carried in the list:** `GameManagerCensusData`
  (`mGameAttributesData` = `GameAttributeCensusData` — population per game-attribute, i.e. games/
  players per mode) and `PlaygroupCensusData` (`Blaze::Playgroups::PlaygroupCensusData` — population
  per playgroup).
- **Errors:** `CENSUSDATA_ERR_PLAYER_{ALREADY_SUBSCRIBED,NOT_SUBSCRIBED}`.

**⇒ WHAT IT IS:** a subscription-based **live population feed**. A client subscribes; the server
pushes `NotifyServerCensusData` carrying `mCensusDataList` — a vector of census items (per-mode game
counts via `GameManagerCensusData`, per-playgroup counts via `PlaygroupCensusData`, plus region
counts). It drives **server-browser / matchmaking population displays** ("N players online",
games-in-progress by mode/region). **NOT used in single-player** — the client never subscribes on the
solo path, so ReCap can leave component 0x0A unimplemented with no client impact. Field-level TDF
offsets (if multiplayer population is ever built) = decompile the client's `NotifyServerCensusData`
parser; these are standard EA Blaze SDK TDFs already partially known to `Tdf.cs`.
