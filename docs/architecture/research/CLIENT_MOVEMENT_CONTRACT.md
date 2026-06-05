# Client movement contract — Ghidra findings (2026-06-05)

How the retail client (Darkspore.exe, image base 0x400000) consumes the server's movement
packets, traced after D-015 round-1 (`0x91 flags=0x001` alone) failed to move the hero.
All addresses verified by decompilation this session and annotated in the Ghidra project
(renamed + plate comments).

## kGms dispatch (RakNet game messages)

- Dispatcher: `0x0053fbb0` — switch on internal msgId-6, byte-table `0x0053feb4`,
  jump-table `0x0053fe10`.
- Internal message ids from the name table @ `0x0118b488` (pairs `{char* name, u32 id}`):
  ObjectCreate=12, ObjectUpdate=13, ObjectDelete=14, ObjectTeleport=16, ObjectJump=17,
  **ObjectPlayerMove=18**, ForcePhysicsUpdate=19, PhysicsChanged=20,
  LocomotionDataUpdate=21, **LocomotionDataUnreliableUpdate=22**, ActionCommandMsgs=29,
  ActionCommandResponse=30.
  ⚠ Internal id ≠ wire opcode minus a constant (ObjectCreate 12↔0x8C but
  ObjectPlayerMove 18↔0x91) — there is a remap in the receive path; identify handlers by
  body size/layout, not by offset arithmetic.

## The handlers (client side)

| Wire | Handler | Body | Gate | Effect |
|------|---------|------|------|--------|
| 0x91 ObjectPlayerMove | `ClientNet::OnGmsObjectPlayerMove` @0x0053e1c0 | 80B (= C++ SendObjectPlayerMove, Server.cpp:1716) | **Local-hero gate**: if objId == `PlayerCtrl::GetControlledHeroObjectId()` @0x004e8f40 (player+0x12e0) and `!PlayerCtrl::IsHeroExternallyControlled()` @0x004e2dd0 → **packet IGNORED** | Stores goalFlags/goal/facing/… into locomotion component (object+0x298, goalFlags @+0x144, goal @+0x148); the client-side Locomotion sim then walks the object smoothly |
| 0x90 ObjectTeleport | `ClientNet::OnGmsObjectTeleport` @0x0053e0e0 | 32B | **None** | `Locomotion::Stop` @0x00a1a150 + set position unconditionally; if local hero, also input/camera resync `FUN_004e2ec0(1)` |
| LocomotionDataUnreliableUpdate | `ClientNet::OnGmsLocomotionDataUnreliableUpdate` @0x0053e600 | 16B (objId + vec3 goal) | **None** | Writes goalPosition (+0x148) AND partialGoalPosition (+0x154) into the locomotion component for ANY object — the sim walks it |
| LocomotionDataUpdate | `ClientNet::OnGmsLocomotionDataUpdate` @0x0053e6f0 | reflection-encoded | None | Reflection-deserializes into the locomotion component (`FUN_00a23270`) |
| ForcePhysicsUpdate | `ClientNet::OnGmsForcePhysicsUpdate` @0x0053e380 | 40B (= C++ SendForcePhysicsUpdate, Server.cpp:1735, BitStream(41)) | none seen | objId + 3×vec3 |

(Correction 2026-06-05: 0x0053e380 was briefly mislabeled ObjectJump; ObjectJump (kGms id 17)
is @0x0053dee0. Full client handler table: `docs/architecture/flow/COMMAND_MATRIX.md`.)

## Client has the full Locomotion class

The C++ reference's `Locomotion.cpp` is a reimplementation of the client's own class:
`Locomotion::SetGoalPosition` @0x00a19bb0 (flags=0x001),
`Locomotion::SetGoalPositionWithDistance` @0x00a19ca0,
`Locomotion::SetGoalObject` @0x00a19da0, `Locomotion::Stop` @0x00a1a150 (flags=0x020,
exact mirror of Locomotion.cpp:564), plus already-mapped `MoveTowardPoint`,
`MoveToPointExact`, `MoveToCircleEdge`, `MoveToPointWithinRange`, `MoveToObject`,
`ClearTargetObject`. goalFlags semantics match C++ (0x001 goal, 0x002 facing, 0x020
teleport/stop, 0x800 speed-mode — checked in `FUN_009eb260` speed setup, which reads noun
locomotion tuning at +0x130 with hardcoded defaults as fallback).

## Local hero input path (why round-1 didn't move)

Click → `FUN_004d9150` (command issue) → `PlayerCtrl::ValidateCommandRoute` @0x004e8bb0
(ability validation: mana/cooldown/range, error codes -0x27xx) which routes:

- **return 1** — server-deferred: command bookkeeping + deadline `now+3000ms`; client
  waits for the server to act. No local movement.
- **return 3** — local execution (gated on command-def byte `+0x151`): calls
  `Locomotion::SetGoalPositionWithDistance` / `SetGoalObject` directly → immediate local
  walk.

Round-1 observed behavior (client sent ActionCommand, never moved, reported position
frozen) = the deferred path: our `0x91 flags=0x001` reply was then dropped by the
local-hero gate, the 3s deadline expired, nothing moved. The working binary's
`teleportMovement=true` contract works because 0x90 bypasses the gate.

## Consequences for ReCap

1. **Current teleport contract (D-015, verified) is correct** for this client build:
   0x90 (pos=goal, quat=0) + 0x91 flags=0x21 per click.
2. **0x91 flags=0x001 will never move the local hero** under normal control — by design.
   It IS the smooth-walk channel for all OTHER objects (enemies, other players) —
   Phase 2b enemy movement can rely on it.
3. **Smooth local-hero movement candidate**: `LocomotionDataUnreliableUpdate` (16B
   objId+goal) is ungated and seeds the local sim — the likely original-server channel
   (C++ has `SendLocomotionDataUnreliableUpdate` disabled behind `#if`,
   Instance.cpp:976). Experiment: reply to Movement ActionCommands with it instead of
   teleport; client should walk smoothly and send Stop(4) on arrival (arrival detection
   is client-side — observed in logs).
4. Open (needs runtime trace, not static): which branch `ValidateCommandRoute` takes for
   Movement in our session (trace return value @0x004e8bb0 while clicking), and what the
   command-def `+0x151` byte is for the movement command — would tell whether the client
   can self-walk without any server packet.

Wire ground truth for the verified teleport contract: `captures/cpp_loopback.pcapng`
(dump via `dump_moves.py`). Ledger: D-015.
