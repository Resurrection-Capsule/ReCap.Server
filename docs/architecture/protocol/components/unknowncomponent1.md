# UnknownComponent1 — 0x2678

C#-only mystery Blaze component. Handles a single command `0x200` as a silent no-op. **Identity and purpose unconfirmed** — not present in C++ reference, absent from working client logs, not routed by client on solo path.

Audit pass 2026-05-29: verified against C++ `Blaze/Component.cpp` (enum + ComponentManager),
full C++ source tree grep (zero matches for `0x2678`, `9880`, `2678`), C# implementation
`Adapters/Blaze/Component/UnknownComponent1.cs`, and component registration in
`BlazeServer.cs`. Tags: `[V]` = verified in cited source, `[?]` = unverified / needs Ghidra.

---

## Component identity

- **C# registration:** `Id => 0x2678` (9880 decimal). Instantiated and attached to lobby Blaze dispatcher. `[V]` `UnknownComponent1.cs:7`, `BlazeServer.cs:71`
- **C++ absence:** Component ID `0x2678` does NOT appear in the C++ reference. Enum `ComponentType` lists only historical/unused ids: Stats 0x07, CensusData 0x0A, Clubs 0x0B, GameReporting 0x1C, RSP 0x801, Teams 0x816. `ComponentManager::Get` wires only 9 live handlers (Auth/GameManager/Redirector/Playgroups/Util/Messaging/Rooms/Association/UserSession); all others return `nullptr`. `[V]` `ReCapCpp/darkspore_server/source/Blaze/Component.cpp:16-94`
- **Zero hits in C++ source tree.** Grep `/darkspore_server/source` for `0x2678`, `9880`, `2678` → no matches. `[V]`
- **Fallback name.** C# class name "UnknownComponent1" mirrors the C++ base `Component::GetName()` fallback, which literally returns `"UnknownComponent"`. `[V]` `Component.cpp:32-34`. The name is a placeholder, not a decoded real identity.
- **No other unknown siblings.** Only `UnknownComponent1.cs` exists in the component directory; no `UnknownComponent2` or variants. `[V]`

---

## Request/response commands

| Command | Cmd ID | C++ handler | C# handler | Status |
|---|---|---|---|---|
| (unknown) | 0x200 | — (no ref) | `UnknownComponent1.cs:23–26` → `return true;` | ⚠️ no-op |

Handler internals: `HandlePacket` dispatches `0x200` to `HandleUnknownPacket1()`, which accepts
(returns `true`) but sends no reply. All other commands hit `default`, log a debug message, and
return `false`. `[V]` `UnknownComponent1.cs:10–26`

---

## Notifications

None sent. Component has no notification dispatch. `[V]` `UnknownComponent1.cs:28–35`

---

## Solo-path relevance

- **NOT observed on solo login.** The complete working C++ server session log (597 lines, covers
  Redirector → Auth → GameManager → Dungeon entry) contains zero references to `0x2678`, `9880`,
  or "Unknown" components. Traffic is only Redirector, Util, Authentication, UserSessions,
  AssociationLists, Rooms, GameManager. `[V]`
- **No blocking risk today.** If command `0x200` is ever sent, C# accepts it (`return true`), so no
  protocol error fires. Deprioritized for dungeon crashes.
- **Latent risk:** If `0x200` requires a response TDF payload and one is never sent, the client may
  stall waiting. Evidence: not on solo path today, so risk = deployment/DLC/future features.

---

## Open questions / Ghidra TODO

- `[?]` **What is component 0x2678 (9880)?** Not in C++ enum, not in C++ source, not in working log. Most plausible = a title-specific or SDK framework component the retail client includes but the C++ reference (hand-curated for minimal scope) never implemented. Confirm via Ghidra: xref the literal `0x2678` in the client's Blaze dispatch/sender and read nearby component-name strings or class definitions.
- `[?]` **Is command 0x200 ever sent?** Absent from working solo log; may only fire in code paths not exercised (multiplayer, DLC, error recovery). Confirm via live packet capture or static analysis of all client command-send sites.
- `[?]` **Does 0x200 need a response TDF or bare acknowledgement?** Current no-op sends nothing. If required, replace handler to construct and send a minimal reply (likely an ERR_OK or empty TDF).
- `[?]` **Ghidra MCP:** Use once the retail client (`Darkspore.exe`) is loaded under a stable project name. Search for kGms-style command tables or Blaze::Component registry to decode the `0x2678` / `0x200` mapping.
