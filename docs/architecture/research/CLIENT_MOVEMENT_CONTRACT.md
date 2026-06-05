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

## ★ ActionCommandResponse type 8 = "movement GO" (2026-06-05 — THE smooth-move protocol)

`ClientNet::OnGmsActionCommandResponse` @0x0053cb10 (wire 0xA8, 56B body) routes on
**byte +1 = response type** (`FUN_004d9ba0`):

| type | effect |
|------|--------|
| 1 | ability ack — arms pending ability (abilityId@+4, u64 times @+0x10/+0x18/+0x20/+0x28, userData@+0x34); matches C++ `SendActionCommandResponse(AbilityCommandResponse)` (Server.cpp:1659, 57B BitStream) |
| 2 | command finished — clears pending, stops anim (`FUN_004f7980(…, 1.0)`) |
| 4 | cancel pending ability |
| **8** | **movement GO**: takes the goal/stop-distance the client STASHED at click time (pending struct +0x40 goal, +0x3c targetId, +0x50 distance) and calls `Locomotion::SetGoalPositionWithDistance(localHero, goal, dist)` (or `SetGoalObject` if targetId set) → **smooth local walk**. Goal does NOT come from the packet. |
| 0x10 | clear (FUN_004e21c0) |

**Retail smooth-movement flow:** click → command deferred (ValidateCommandRoute=1,
goal stashed, 0x9C sent, 3s deadline) → server replies 0xA8 type=8 → client walks
smoothly to its own stashed goal → client sends Stop(4) on arrival. The C++ reference
KNOWS this packet (sends type 1 for abilities; its comments cite these exact client
functions) but never implemented type 8 — that's why it fell back to teleportMovement.

**ReCap experiment (D-016 candidate):** on ActionCommand type=3, reply 0xA8 56B:
`[u8 cmdByte0][u8 8][u8 0][u8 0]` + 52 zero bytes (case 8 reads only the stash; byte0
is stored, not gated) — possibly instead of (or before) the 0x90 teleport. Expected:
smooth walking. Verify byte0 semantics on wire first (C++ sends `*param_1` = the
matching pending-slot byte; client stores it at pending+0x38).

## Client Locomotion class — full mapped API (2026-06-05)

All matched 1:1 vs C++ `Locomotion.cpp` (which is a reimpl of this class) and renamed:
SetGoalPosition @0x00a19bb0 (0x001) · SetGoalPositionWithDistance @0x00a19ca0 ·
SetGoalObject @0x00a19da0 · SetGoalObjectEx @0x00a19f20 (0x400|0x40) · SetFacing
@0x00a1a9f0 (0x042) · Stop @0x00a1a150 (0x020) · TurnToFaceTargetObject @0x00a1ab80
(0x102) · MoveToPointWhileFacingTarget @0x00a1a060 (0x101) · ClearExternalVelocity
@0x00a1a2f0 (0x020+zero extVel) · ClearTargetObject @0x009fb0a2 (&~0x40,&~0x100) ·
plus MoveTowardPoint/MoveToPointExact/MoveToCircleEdge/MoveToPointWithinRange/
MoveToObject (earlier sessions). Component layout (object+0x298): +0x90 targetId,
+0x144 goalFlags, +0x148 goalPos, +0x154 partialGoalPos, +0x178 facingDir,
+0x184 extLinVel, +0x19c/+0x1a0 allowed/desired stop, +0x1ac targetPos. goalFlags
semantics confirmed = the C++ Locomotion.cpp:20-31 table.

Wire ground truth for the verified teleport contract: `captures/cpp_loopback.pcapng`
(dump via `dump_moves.py`). Ledger: D-015.
