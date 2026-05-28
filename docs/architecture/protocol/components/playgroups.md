# Playgroups Component — 0x06

Manages pre-game party (playgroup) sessions: creation, member management, and join/leave lifecycle, on the Blaze lobby connection (port 42125).

---

## Request/response commands

| Command name | Cmd ID | Direction | C++ handler | C# handler | Status |
|---|---|---|---|---|---|
| createPlaygroup | 0x01 | C→S | `Blaze/Component/PlaygroupsComponent.cpp:274` (stub) | `Adapters/Blaze/Component/PlaygroupsComponent.cs:24` | ⚠️ |
| destroyPlaygroup | 0x02 | C→S | `Blaze/Component/PlaygroupsComponent.cpp:278` (stub) | — | ❌ |
| joinPlaygroup | 0x03 | C→S | `Blaze/Component/PlaygroupsComponent.cpp:282` (stub) | — | ❌ |
| leavePlaygroup | 0x04 | C→S | `Blaze/Component/PlaygroupsComponent.cpp:286` (stub) | — | ❌ |
| setPlaygroupAttributes | 0x05 | C→S | `Blaze/Component/PlaygroupsComponent.cpp:290` (stub) | — | ❌ |
| setMemberAttributes | 0x06 | C→S | `Blaze/Component/PlaygroupsComponent.cpp:294` (stub) | — | ❌ |
| kickPlaygroupMember | 0x07 | C→S | `Blaze/Component/PlaygroupsComponent.cpp:298` (stub) | — | ❌ |
| setPlaygroupJoinControls | 0x08 | C→S | `Blaze/Component/PlaygroupsComponent.cpp:302` (stub) | — | ❌ |
| finalizePlaygroupCreation | 0x09 | C→S | `Blaze/Component/PlaygroupsComponent.cpp:306` (stub) | — | ❌ |
| lookupPlaygroupInfo | 0x0A | C→S | `Blaze/Component/PlaygroupsComponent.cpp:310` (stub) | — | ❌ |
| resetPlaygroupSession | 0x0B | C→S | `Blaze/Component/PlaygroupsComponent.cpp:314` (stub) | — | ❌ |

## Notifications sent (S→C)

| Notification name | Notify ID | Direction | C++ sender | C# sender | Status |
|---|---|---|---|---|---|
| NotifyDestroyPlaygroup | 0x32 | S→C | `Blaze/Component/PlaygroupsComponent.cpp:138` | — | ❌ |
| NotifyJoinPlaygroup | 0x33 | S→C | `Blaze/Component/PlaygroupsComponent.cpp:146` | — | ❌ |
| NotifyMemberJoinedPlaygroup | 0x34 | S→C | `Blaze/Component/PlaygroupsComponent.cpp:181` | — | ❌ |
| NotifyMemberRemovedFromPlaygroup | 0x35 | S→C | `Blaze/Component/PlaygroupsComponent.cpp:202` | — | ❌ |
| NotifyPlaygroupAttributesSet | 0x36 | S→C | `Blaze/Component/PlaygroupsComponent.cpp:211` | — | ❌ |
| NotifyMemberAttributesSet | 0x4B | S→C | `Blaze/Component/PlaygroupsComponent.cpp:222` | — | ❌ |
| NotifyLeaderChange | 0x4F | S→C | `Blaze/Component/PlaygroupsComponent.cpp:234` | — | ❌ |
| NotifyMemberPermissionsChange | 0x50 | S→C | `Blaze/Component/PlaygroupsComponent.cpp:243` | — | ❌ |
| NotifyJoinControlsChange | 0x55 | S→C | `Blaze/Component/PlaygroupsComponent.cpp:252` | — | ❌ |
| NotifyXboxSessionInfo | 0x56 | S→C | `Blaze/Component/PlaygroupsComponent.cpp:260` | — | ❌ |
| NotifyXboxSessionChange | 0x57 | S→C | `Blaze/Component/PlaygroupsComponent.cpp:270` | — | ❌ |

---

## Key TDF fields — createPlaygroup (0x01)

C++ body is a stub with no reads; C# reads `PlaygroupInfo` from the request and echoes it back in `JoinPlaygroupResponse`.

**Response (JoinPlaygroupResponse):**

| Tag | Type | Description |
|---|---|---|
| `INFO` | struct | PlaygroupInfo (PGID, name, owner, join state, member limit, network topology, UUID) |

**NotifyJoinPlaygroup (0x33) body (C++ only):**

| Tag | Type | Description |
|---|---|---|
| `INFO` | struct | PlaygroupInfo (enbv, hostSlotId, state, pres, ownerId, ntop, pgid, uuid) |
| `MLST` | list&lt;struct&gt; | Member list (empty for solo) |
| `USER` | u64 | User ID of joining player |

---

## Porting gaps

- All 11 request commands in C++ are stubs with empty bodies. C# implements only `createPlaygroup` (echoes back the request's `PlaygroupInfo`).
- All 11 C++ notifications have body-writing code but are never triggered because the request handlers are stubs; none are implemented in C#.
- The playgroup flow (`createPlaygroup` → `NotifyJoinPlaygroup` → `NotifyMemberJoinedPlaygroup`) is on the critical path between `loginPersona` and `resetDedicatedServer` in standard Darkspore client flow. The current C# stub satisfies the client enough to proceed.
- `NotifyXboxSessionInfo`/`NotifyXboxSessionChange` are Xbox-specific and not relevant for the PC target.
