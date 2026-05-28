# UnknownComponent1 — 0x2678

Unidentified Blaze component that handles a single observed command (`0x200`). C#-only; no C++ counterpart in the reference implementation.

**C# only.** No corresponding C++ source file exists in the reference tree.

---

## Request/response commands

| Command name | Cmd ID | Direction | C++ handler | C# handler | Status |
|---|---|---|---|---|---|
| (unknown) | 0x200 | C→S | — | `Adapters/Blaze/Component/UnknownComponent1.cs:23` (no-op) | ⚠️ |

## Notifications sent

None known.

---

## Notes

- Component ID `0x2678` does not map to any named Blaze component in the C++ reference or known Blaze SDK documentation.
- The single handled command (`0x200`) returns `true` without sending any response packet — effectively a silent drop.
- The component was likely added after observing the client sending this component ID without a matching handler, causing log noise.
- No TDF fields are read or written.

---

## Porting gaps

- Purpose unknown. Research required: packet capture to identify what the client sends at `0x2678/0x200` and when in the login flow it occurs.
- If the command requires a response, the current no-op handler will leave the client waiting.
