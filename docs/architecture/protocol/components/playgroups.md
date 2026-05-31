# Playgroups Component — 0x06

Manages pre-game party (playgroup) sessions: creation, member management, and join/leave lifecycle
on the Blaze lobby connection (port 42125). Playgroups establish peer/dedicated-server topology
for dungeon runs, define member limits, join visibility (open/closed), and host slot assignment.
Critical path between Authentication and GameManager setup.

Audit pass 2026-05-29: verified against C++ `Blaze/Component/PlaygroupsComponent.cpp`,
`Blaze/Component/PlaygroupsComponent.h`, `Blaze/Types.h` (enum definitions), `Blaze/Functions.h`
(TDF writers), and C# port `Adapters/Blaze/Component/PlaygroupsComponent.cs`. Tags: `[V]` =
verified in cited source, `[?]` = unverified / needs Ghidra. Ghidra was **not reachable** this pass
(no instance).

---

## Request/response commands

| Command | Cmd ID | C++ handler | C# handler | Status |
|---|---|---|---|---|
| createPlaygroup | 0x01 | `PlaygroupsComponent.cpp:274` | `PlaygroupsComponent.cs:24` | ⚠️ stub echo |
| destroyPlaygroup | 0x02 | `PlaygroupsComponent.cpp:278` | — | ❌ |
| joinPlaygroup | 0x03 | `PlaygroupsComponent.cpp:282` | — | ❌ |
| leavePlaygroup | 0x04 | `PlaygroupsComponent.cpp:286` | — | ❌ |
| setPlaygroupAttributes | 0x05 | `PlaygroupsComponent.cpp:290` | — | ❌ |
| setMemberAttributes | 0x06 | `PlaygroupsComponent.cpp:294` | — | ❌ |
| kickPlaygroupMember | 0x07 | `PlaygroupsComponent.cpp:298` | — | ❌ |
| setPlaygroupJoinControls | 0x08 | `PlaygroupsComponent.cpp:302` | — | ❌ |
| finalizePlaygroupCreation | 0x09 | `PlaygroupsComponent.cpp:306` | — | ❌ |
| lookupPlaygroupInfo | 0x0A | `PlaygroupsComponent.cpp:310` | — | ❌ |
| resetPlaygroupSession | 0x0B | `PlaygroupsComponent.cpp:314` | — | ❌ |

All 11 C++ handlers are empty stubs (lines 274–316). `[V]` `PlaygroupsComponent.cpp:274-316`
Dispatch occurs in `ParsePacket()` switch. `[V]` `PlaygroupsComponent.cpp:85-136`

---

## Notifications sent (S→C)

| Notification | Notify ID | C++ sender | C# sender | Status |
|---|---|---|---|---|
| NotifyDestroyPlaygroup | 0x32 | `PlaygroupsComponent.cpp:138` | — | ❌ |
| NotifyJoinPlaygroup | 0x33 | `PlaygroupsComponent.cpp:146` | — | ❌ |
| NotifyMemberJoinedPlaygroup | 0x34 | `PlaygroupsComponent.cpp:181` | — | ❌ |
| NotifyMemberRemovedFromPlaygroup | 0x35 | `PlaygroupsComponent.cpp:202` | — | ❌ |
| NotifyPlaygroupAttributesSet | 0x36 | `PlaygroupsComponent.cpp:211` | — | ❌ |
| NotifyMemberAttributesSet | 0x4B | `PlaygroupsComponent.cpp:222` | — | ❌ |
| NotifyLeaderChange | 0x4F | `PlaygroupsComponent.cpp:234` | — | ❌ |
| NotifyMemberPermissionsChange | 0x50 | `PlaygroupsComponent.cpp:243` | — | ❌ |
| NotifyJoinControlsChange | 0x55 | `PlaygroupsComponent.cpp:252` | — | ❌ |
| NotifyXboxSessionInfo | 0x56 | `PlaygroupsComponent.cpp:260` | — | ❌ |
| NotifyXboxSessionChange | 0x57 | `PlaygroupsComponent.cpp:270` | — | ❌ |

All 11 notifications have fully-implemented TDF writers in C++ but **never called** from request
handlers (stubs). None implemented in C#. `[V]` `PlaygroupsComponent.cpp:138-272`

---

## What each handler does (internals)

### createPlaygroup (0x01) `[V]`
- C++: empty stub, no reads. `PlaygroupsComponent.cpp:274`
- C#: reads `CreatePlaygroupRequest{Join, PlaygroupInfo}` (TDF unmarshalled); replies with
  `JoinPlaygroupResponse{PlaygroupInfo}` echoing the request's `Info` struct.
  `PlaygroupsComponent.cs:24-34`
- **Semantics diverge:** C++ stubs offer no insight; C# echo satisfies client enough to proceed to
  next phase (NotifyJoinPlaygroup-less path). `[?]` Does client expect `NotifyJoinPlaygroup`
  after reply, or is echo sufficient?

### destroyPlaygroup / joinPlaygroup / leavePlaygroup (0x02–0x04) `[V]`
- All C++ stubs (empty bodies). `PlaygroupsComponent.cpp:278-288`
- All C# unimplemented (no case in switch). `PlaygroupsComponent.cs:13-21`

### Other handlers (0x05–0x0B) `[V]`
- All C++ stubs. `PlaygroupsComponent.cpp:290-316`
- All C# unimplemented.

---

## Notification details (C++ only, never fired)

### NotifyJoinPlaygroup (0x33) `[V]`
- Hardcoded playgroup: PGID=1, OWNR=userId, NAME="Test playgroup",
  UUID="71bc4bdb-82ec-494d-8d75-ca5123b827ac", NTOP=ClientServerDedicated, MLIM=1,
  HSID=0, PRES=Standard, ENBV=true, UPRS=true, UKEY="what".
  `PlaygroupsComponent.cpp:146-179`
- TDF: INFO struct + MLST (empty member list) + USER.
- **Never called** — C++ stub doesn't fire it.

### NotifyMemberJoinedPlaygroup (0x34) `[V]`
- Hardcoded member: slot=0, permissions=0, jtim=0, user=Unknown/id=0.
  `PlaygroupsComponent.cpp:181-200`
- TDF: MEMB struct (user + slot + permissions) + PGID.
- **Never called.**

### Other notifications `[V]`
- NotifyDestroyPlaygroup (0x32): PGID + REAS. `PlaygroupsComponent.cpp:138-144`
- NotifyMemberRemovedFromPlaygroup (0x35): MLST + PGID + REAS. `PlaygroupsComponent.cpp:202-209`
- NotifyPlaygroupAttributesSet (0x36): ATTR map + PGID. `PlaygroupsComponent.cpp:211-220`
- NotifyMemberAttributesSet (0x4B): ATTR map + EID + PGID. `PlaygroupsComponent.cpp:222-232`
- NotifyLeaderChange (0x4F): HSID + LID + PGID. `PlaygroupsComponent.cpp:234-241`
- NotifyMemberPermissionsChange (0x50): LID + PERM + PGID. `PlaygroupsComponent.cpp:243-250`
- NotifyJoinControlsChange (0x55): OPEN (enum PlaygroupJoinState) + PGID. `PlaygroupsComponent.cpp:252-258`
- NotifyXboxSessionInfo (0x56): PGID + PRES + XNNC blob + XSES blob. `PlaygroupsComponent.cpp:260-268`
- NotifyXboxSessionChange (0x57): calls NotifyXboxSessionInfo. `PlaygroupsComponent.cpp:270-272`
- **All never called.**

---

## Key TDF field tables

### PlaygroupInfo struct (in createPlaygroup request + JoinPlaygroupResponse + NotifyJoinPlaygroup) `[V]` `Functions.cpp:512-539`

| Tag | Type | C++ member | C++ init value (NotifyJoinPlaygroup) | C# property (PlaygroupInfo) |
|---|---|---|---|---|
| ATTR | map(int→str) | attributes | {} | PlaygroupAttributes |
| ENBV | bool | enbv | true | EnableVoIP |
| HNET | union | (unused) | NetworkAddressMember::Unset | HostNetworkAddress |
| HSID | u32 | hostSlotId | 0 | HostSlotId |
| JOIN | enum | state | PlaygroupJoinState::Open (0) | PlaygroupJoinability |
| MLIM | u32 | memberLimit | 1 | MaxMembers |
| NAME | string | name | "Test playgroup" | Name |
| NTOP | enum | ntop | GameNetworkTopology::ClientServerDedicated | NetworkTopology |
| OWNR | u64 | ownerId | userId | OwnerBlazeId |
| PGID | u32 | playgroupId | 1 | PlaygroupId |
| PRES | enum | pres | PresenceMode::Standard (1) | PresenceMode |
| UKEY | string | ukey | "what" | UniqueKey |
| UPRS | bool | uprs | true | HasPresence |
| UUID | string | uuid | "71bc4bdb-82ec-494d-8d75-ca5123b827ac" | UUID |
| VOIP | enum | (inline) | VoipTopology::Disabled (0) | VoipNetwork |
| XNNC | blob | (unused) | null | XnetNonce |
| XSES | blob | (unused) | null | XnetSession |

C++ `PlaygroupInfo::Write` order differs from field definition order; C# uses TdfField
attributes (order implicit in serialization). `[V]` `PlaygroupInfo.cs:94-145`

### PlaygroupMemberInfo struct (in NotifyMemberJoinedPlaygroup) `[V]` `Functions.cpp:542-561`

| Tag | Type | C++ member | C++ init (NotifyMemberJoinedPlaygroup) | C# counterpart |
|---|---|---|---|---|
| ATTR | map(int→str) | attributes | {} | — |
| JTIM | u32 | jtim | 0 | — |
| PERM | u32 | permissions | 0 | — |
| PNET | union | (unused) | NetworkAddressMember::Unset | — |
| SID | u8 | slot | 0 | — |
| USER | struct | user (UserIdentification) | id=0, name="Unknown", localization=0 | — |

No C# counterpart. `[?]` User typedef unknown; likely PlayerIdentification in Blaze.Types.

### Enum value mappings `[V]` `Types.h:179-181, 83-87, 95-101, 89-93`

**PlaygroupJoinState:**
- Open = 0
- Closed = 1

**PresenceMode:**
- None = 0
- Standard = 1
- Private = 2

**GameNetworkTopology:**
- ClientServerPeerHosted = 0
- ClientServerDedicated = 1
- PeerToPeerFullMesh = 0x82
- PeerToPeerPartialMesh = 0x83
- PeerToPeerDirtyCastFailover = 0x84

**VoipTopology:**
- Disabled = 0
- DedicatedServer = 1
- PeerToPeer = 2

C# enums match C++ semantically (same names / numeric values). `[V]` `PlaygroupInfo.cs:87-91, 116-117, 137-138, 125-126`

---

## CreatePlaygroupRequest TDF structure `[V]` `PlaygroupsComponent.cs:78-85`

| Tag | Type | C# property |
|---|---|---|
| JOIN | bool | Join |
| PGRP | struct PlaygroupInfo | Info |

Client marshals this; C# unmarshals and echoes back in reply (minus JOIN flag). `[V]` `CreatePlaygroupRequest:78-85`

---

## Divergences (C++ vs C#)

1. **C++ stubs have no logic.** All 11 request handlers in C++ are empty (lines 274–316);
   dispatch exists but no reads/replies. `[V]` `PlaygroupsComponent.cpp:274-316`
2. **C# implements only createPlaygroup (0x01) as a stub echo.** Takes the request's PlaygroupInfo,
   replies with it in JoinPlaygroupResponse. No persistence, no playgroup DB, no lifecycle.
   `[V]` `PlaygroupsComponent.cs:24-34`
3. **C++ notifications fully built but never called.** All TDF writers in place; NotifyJoinPlaygroup
   hardcodes a test playgroup (PGID=1, "Test playgroup"). Because request handlers are empty stubs,
   no notification path exists. C# has no notify mechanism at all.
   `[V]` `PlaygroupsComponent.cpp:146-179 vs PlaygroupsComponent.cs:56-73`
4. **PlaygroupInfo field ordering.** C++ Write() emits fields in TDF-writer order (ATTR, ENBV,
   HNET, HSID, …); C# TdfField attributes define order implicitly during serialization. Both
   should round-trip correctly if TDF deserializer is order-agnostic (TDF maps use tags, not
   positions). `[V]` `Functions.cpp:512-539 vs PlaygroupInfo.cs:94-145`
5. **HNET / PNET unions unused.** Both C++ writers push an Unset union (placeholder); C# defines
   the field as NetworkAddress (unmarshalled but empty). Never validated against real client
   packets. `[?]`
6. **Xbox-specific fields present but unused.** XNNC (Xbox Nonce), XSES (Xbox Session) blobs in
   PlaygroupInfo; XNNC/XSES in NotifyXboxSessionInfo struct. Darkspore PC target ignores these.
   `[V]` `Functions.cpp:537-538, PlaygroupInfo.cs:140-144`
7. **NotifyXboxSessionInfo / NotifyXboxSessionChange duplicated.** NotifyXboxSessionChange simply
   calls NotifyXboxSessionInfo (0x57 fires 0x56). `[V]` `PlaygroupsComponent.cpp:270-272`

---

## Open questions / Ghidra TODO

- `[?]` C++ `CreatePlaygroup` is a stub with no reads. Does the client send a playgroup request,
  and if so, what does it contain? Verify against C++ runtime logs or Ghidra client handler.
- `[?]` Does the client expect a `NotifyJoinPlaygroup` after `createPlaygroup` reply, or is the
  echo response sufficient to proceed to GameManager?
- `[?]` C# `JoinPlaygroupResponse{Info}` has no MLST (member list). Should the reply populate MLST
  with the player's own entry, or is it only sent in the notification?
- `[?]` Confirm HNET (host network) and PNET (peer network) union structure / required fields.
  Are these for peer addresses, or stubs for future use?
- `[?]` NotifyMemberJoinedPlaygroup hardcodes member user id=0, name="Unknown". Should this reflect
  the joining player? Never fired in C++, so untested.
- `[?]` PlaygroupJoinState::Closed (1) is defined but never set; open=0 hardcoded everywhere. When
  does a playgroup close?
- `[?]` Verify whether C# playgroup flow (echo with no persistence) genuinely allows client to
  proceed, or if a later component expects playgroup DB state (e.g., GameManager checking member
  limits or ownership).
- `[?]` Map NotifyJoinPlaygroup firing point in live client: does it fire after user sends
  createPlaygroup, or is it autonomous server-side?
