# Lua Natives — Wave 2 (Locomotion) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the 9 locomotion-subsystem demanded Lua natives so ability/movement tick scripts drive NPC movement, using the client-verified `0x95 LocomotionDataUnreliableUpdate` channel for visible smooth movement.

**Architecture:** Natives (`ReCap.Server/Adapters/Scripting/Api/`) mutate server-side locomotion state on `GameObject` and mark it `Locomotion`-dirty; the existing per-tick `Game.FlushObjectUpdates` broadcasts it — extended to emit the verified `0x95` packet (objId + goalPosition) when a goal is set, keeping `0x90` teleport (stop bit) and `0x94` GoalFlags fallback. Game-facing mutation goes through new `IScriptGameBridge` methods (impl `GameScriptContext`, fakes `FakeBridge`/`MutableHpBridge`). Because the server does not integrate movement physics yet, the two "wait for arrival" natives resume on a **time estimate** (distance ÷ move speed, capped by timeout), not a live position predicate.

**Tech Stack:** C# net10.0, native lua 5.1.4 (float ABI) P/Invoke, xUnit.

## Global Constraints

- Target framework **net10.0**; native lua `LUA_NUMBER = float` — pushed numbers are `(float)`.
- Every native body is exception-proof: try/catch, push fallback / return N. `lua_yield` is a longjmp — `return lua_yield(...)` MUST be OUTSIDE the managed try (mirror `WaitForXSeconds`).
- Wire is LE. **`0x95 LocomotionDataUnreliableUpdate` body = `ObjectId (u32 LE)` + `GoalPosition.X, .Y, .Z` (3× float32 LE) = 16 bytes**, verified from the client parse `ClientNet::OnGmsLocomotionDataUnreliableUpdate @0x0053e600` (reads 0x10 bytes: objId + vec3; writes goalPosition→comp+0x148 and partialGoalPosition→comp+0x154; no hero gate). Do not add or reorder fields.
- Locomotion goal semantics mirror C++ `Locomotion::SetGoalPosition` (`GoalFlags=0x001`, clears target/facing/velocity) and `Locomotion::Stop` (`GoalFlags=0x020`), already in `LocomotionData.cs:158,170`.
- No code comments except a short verified cite (Ghidra `addr` / C++ `file:line` / catalog). No AI attribution anywhere.
- No hardcoded values the game derives from data — where a data source (per-noun move speed / jump duration) is not yet parsed, use a named constant with an explicit `DEFERRED:` cite, never a bare literal.
- Build/test on Windows; close the server before rebuilding. Tests: `dotnet test ReCap.Tests/ReCap.Tests.csproj`. Ratchet: `RECAP_HARVEST=1 dotnet test --filter NativeDemandHarvest` — `demanded` count may only decrease.

## Scope

**In (9 natives):** nLocomotion.Stop, nLocomotion.SlideToPoint, nLocomotion.MoveToCircleEdge, nLocomotion.TurnToFace, nGameObject.SetTargetPosition, nGameObject.SetNavCollision, nGameObject.GetModifiedMoveSpeed, nThread.WaitForNearGoal, nThread.WaitForJumpComplete. Plus the `0x95` packet + `FlushObjectUpdates` routing + `GameObject` locomotion fields + bridge locomotion API.

**Explicitly deferred (flagged, not silently dropped):**
- Visible turn-in-place: `TurnToFace` sets `GameObject.Facing` server-side only (no wire in Wave 2 — facing broadcast would need the 0x94 reflection builder extended; deferred).
- Per-noun move speed + jump duration: sourced from a named constant with a `DEFERRED:` cite (real source = LocomotionTuning asset, not yet parsed).
- Real arrival detection: no server-side movement integration exists, so WaitForNearGoal/JumpComplete use a time estimate, not a live position predicate.

## File Structure

- `ReCap.Server/Adapters/RakNet/Packets/LocomotionDataUnreliableUpdatePacket.cs` — **new** (0x95, 16-byte body).
- `ReCap.Server/Adapters/RakNet/Packets/PacketActivator.cs:91` — fill the empty `LocomotionDataUnreliableUpdate` case.
- `ReCap.Server/Domain/Gameplay/ObjectManager.cs` — add locomotion fields to `GameObject` + set `MoveSpeed` default in `Spawn`.
- `ReCap.Server/Domain/Gameplay/Game.cs:250-266` — extend `FlushObjectUpdates` to emit 0x95 on goal-set.
- `ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs` — add locomotion methods to `IScriptGameBridge`.
- `ReCap.Server/Services/Scripting/GameScriptContext.cs` — implement them.
- `ReCap.Server/Adapters/Scripting/Api/NLocomotionModule.cs` — **new** module (Stop, SlideToPoint, MoveToCircleEdge, TurnToFace).
- `ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs` — add SetTargetPosition, SetNavCollision, GetModifiedMoveSpeed.
- `ReCap.Server/Adapters/Scripting/Api/NThreadModule.cs` — add WaitForNearGoal, WaitForJumpComplete.
- `ReCap.Server/Adapters/Scripting/LuaRuntime.cs:53-70` — wire `NLocomotionModule.Register(L)`.
- `ReCap.Tests/Packets/LocomotionUnreliablePacketTests.cs` — **new** golden test.
- `ReCap.Tests/Scripting/GameBridgeTests.cs` — extend `FakeBridge` + native tests.
- `ReCap.Tests/Scripting/SchedulerPredicateTests.cs` — extend `MutableHpBridge` for the new interface members; WaitForNearGoal/JumpComplete tests.

---

### Task 1: LocomotionDataUnreliableUpdatePacket (0x95)

**Files:** Create `ReCap.Server/Adapters/RakNet/Packets/LocomotionDataUnreliableUpdatePacket.cs`; Modify `PacketActivator.cs:91`; Test `ReCap.Tests/Packets/LocomotionUnreliablePacketTests.cs`.

**Interfaces:** Produces `LocomotionDataUnreliableUpdatePacket { uint ObjectId; System.Numerics.Vector3 GoalPosition; }` with `Type => PacketType.LocomotionDataUnreliableUpdate` and a 16-byte LE `WriteTo`.

- [ ] **Step 1: Write the failing golden test** — create `LocomotionUnreliablePacketTests.cs`:

```csharp
using System.IO;
using System.Numerics;
using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

public class LocomotionUnreliablePacketTests
{
    [Fact]
    public void WritesObjectIdThenGoalPositionLE16Bytes()
    {
        var packet = new LocomotionDataUnreliableUpdatePacket
        {
            ObjectId = 0x11223344,
            GoalPosition = new Vector3(1.0f, 2.0f, 3.0f),
        };
        using var ms = new MemoryStream();
        packet.WriteTo(ms);
        var bytes = ms.ToArray();

        // 0x95 body (client OnGmsLocomotionDataUnreliableUpdate @0x0053e600): objId u32 LE + vec3 LE.
        Assert.Equal(16, bytes.Length);
        Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, bytes[0..4]);
        Assert.Equal(BitConverter.GetBytes(1.0f), bytes[4..8]);
        Assert.Equal(BitConverter.GetBytes(2.0f), bytes[8..12]);
        Assert.Equal(BitConverter.GetBytes(3.0f), bytes[12..16]);
        Assert.Equal(PacketType.LocomotionDataUnreliableUpdate, packet.Type);
    }
}
```

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter WritesObjectIdThenGoalPositionLE16Bytes`. Expected: FAIL (type does not exist).

- [ ] **Step 3: Implement the packet** — create `LocomotionDataUnreliableUpdatePacket.cs`:

```csharp
using System.IO;
using System.Numerics;
using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// LocomotionDataUnreliableUpdate (0x95). Client handler ClientNet::OnGmsLocomotionDataUnreliableUpdate
// @0x0053e600 (Ghidra): reads a 16-byte body — objId (u32) + vec3 goalPosition — and writes it into the
// object's locomotion component (goalPosition@+0x148, partialGoalPosition@+0x154), no local-hero gate.
// This is the smooth-movement channel for any object. LE per the game protocol.
public class LocomotionDataUnreliableUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.LocomotionDataUnreliableUpdate;
    public uint ObjectId { get; set; }
    public Vector3 GoalPosition { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(ObjectId);
        writer.Write(GoalPosition.X);
        writer.Write(GoalPosition.Y);
        writer.Write(GoalPosition.Z);
    }
}
```

- [ ] **Step 4: Wire the activator** — in `PacketActivator.cs`, replace the empty case body at line 91:

```csharp
            case PacketType.LocomotionDataUnreliableUpdate:
                packet = new LocomotionDataUnreliableUpdatePacket();
                break;
```

- [ ] **Step 5: Run test, verify it passes** — Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter WritesObjectIdThenGoalPositionLE16Bytes`. Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add ReCap.Server/Adapters/RakNet/Packets/LocomotionDataUnreliableUpdatePacket.cs ReCap.Server/Adapters/RakNet/Packets/PacketActivator.cs ReCap.Tests/Packets/LocomotionUnreliablePacketTests.cs
git commit -m "feat(raknet): LocomotionDataUnreliableUpdate 0x95 packet (client-verified 16B objId+goalPos)"
```

---

### Task 2: GameObject locomotion fields + spawn move-speed default

**Files:** Modify `ReCap.Server/Domain/Gameplay/ObjectManager.cs`.

**Interfaces:** Produces on `GameObject`: `Vector3 GoalPosition`, `Vector3 TargetPosition`, `Vector3 Facing`, `float MoveSpeed`, `bool NavCollisionDisabled`. `MoveSpeed` set in `Spawn`.

- [ ] **Step 1: Add the fields** — in `ObjectManager.cs`, in the `GameObject` class after `GoalFlags` (line ~39):

```csharp
    // Locomotion goal state (mirrors C++ Locomotion component). GoalPosition drives the 0x95
    // smooth-move broadcast; TargetPosition is the homing target (SetTargetPosition); Facing is
    // set by TurnToFace (server-side only in Wave 2). NavCollisionDisabled mirrors client obj+0x284
    // (SetNavCollision writes the inverted collidable flag; server-side pathfinding state, no wire).
    public Vector3 GoalPosition { get; set; }
    public Vector3 TargetPosition { get; set; }
    public Vector3 Facing { get; set; }
    // DEFERRED: real per-noun move speed comes from the LocomotionTuning asset (Ghidra
    // LocomotionTuning @0x00f798e0), not yet parsed. Default is a placeholder tuning value used only
    // by GetModifiedMoveSpeed's wind-down estimate; replace when tuning is parsed.
    public float MoveSpeed { get; set; } = DefaultMoveSpeed;
    public bool NavCollisionDisabled { get; set; }

    public const float DefaultMoveSpeed = 5.0f;
```

(Requires `using System.Numerics;` — already present at the top of `ObjectManager.cs`.)

- [ ] **Step 2: Confirm it compiles** — Run: `dotnet build ReCap.Server/ReCap.Server.csproj -v q`. Expected: build succeeds (no test yet — fields are consumed by later tasks).

- [ ] **Step 3: Commit**

```bash
git add ReCap.Server/Domain/Gameplay/ObjectManager.cs
git commit -m "feat(gameplay): GameObject locomotion fields (goal/target/facing/moveSpeed/navCollision)"
```

---

### Task 3: FlushObjectUpdates emits 0x95 on goal-set

**Files:** Modify `ReCap.Server/Domain/Gameplay/Game.cs:250-266`; Test `ReCap.Tests/...` — see note.

**Interfaces:** Consumes `GameObject.GoalPosition`/`GoalFlags`. Produces the broadcast routing: stop bit `0x020` → 0x90 teleport; goal bit `0x001` → 0x95 unreliable (objId+GoalPosition); else → 0x94 GoalFlags fallback.

**Testing note:** `FlushObjectUpdates` is `private` and broadcast goes to connected players (none in a unit test). Rather than broaden visibility, this task is verified by the downstream native gate (Task 5's SlideToPoint sets goal + dirty → this routing produces 0x95) and by reading. Add a focused test ONLY if you make the routing independently callable; do not widen `Game`'s API just to test. If you can reach it via an existing test seam, assert the packet type chosen for a goal-set object; otherwise state in your report that this task is covered by Task 5's integration path and the read-review.

- [ ] **Step 1: Implement the routing** — replace the locomotion block in `FlushObjectUpdates` (`Game.cs:256-262`):

```csharp
            if ((obj.DirtyFlags & ObjectDirtyFlags.Locomotion) != 0 && !obj.PlayerControlled)
            {
                IRakNetPacket locomotionPacket;
                if ((obj.GoalFlags & 0x020) != 0)
                    locomotionPacket = new ObjectTeleportPacket { ObjectId = obj.ObjectId, Position = obj.Position, Orientation = obj.Orientation };
                else if ((obj.GoalFlags & 0x001) != 0)
                    // 0x95 smooth-move channel (client OnGmsLocomotionDataUnreliableUpdate @0x0053e600).
                    locomotionPacket = new LocomotionDataUnreliableUpdatePacket { ObjectId = obj.ObjectId, GoalPosition = obj.GoalPosition };
                else
                    locomotionPacket = new LocomotionDataUpdatePacket { ObjectId = obj.ObjectId, Locomotion = new LocomotionData { GoalFlags = obj.GoalFlags } };
                BroadcastToAllPlayers(locomotionPacket);
            }
```

- [ ] **Step 2: Verify it compiles + full suite green** — Run: `dotnet build ReCap.Server/ReCap.Server.csproj -v q` then `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter Packets`. Expected: build OK, packet tests green (no regression to 0x90/0x94 paths).

- [ ] **Step 3: Commit**

```bash
git add ReCap.Server/Domain/Gameplay/Game.cs
git commit -m "feat(gameplay): FlushObjectUpdates routes goal-set to 0x95 smooth-move"
```

---

### Task 4: IScriptGameBridge locomotion API + implementations

**Files:** Modify `ScriptContextRegistry.cs` (interface), `GameScriptContext.cs` (impl), `GameBridgeTests.cs` (`FakeBridge`), `SchedulerPredicateTests.cs` (`MutableHpBridge`).

**Interfaces:** Produces on `IScriptGameBridge`:
```csharp
void SetLocomotionGoal(uint objectId, float x, float y, float z, float stopDistance);
void SetLocomotionTarget(uint objectId, float x, float y, float z);
void SetFacing(uint objectId, float x, float y, float z);
void StopLocomotion(uint objectId);
void SetNavCollision(uint objectId, bool collidable);
float GetModifiedMoveSpeed(uint objectId);
bool TryGetGoalDistance(uint objectId, out float distance);
```
`TryGetGoalDistance` = straight-line distance from current Position to GoalPosition (for the WaitForNearGoal estimate); false if the object is missing.

- [ ] **Step 1: Write failing tests** — in `GameBridgeTests.cs`, extend `FakeBridge`:

```csharp
    // locomotion (Wave 2)
    public (uint Id, float X, float Y, float Z, float Stop)? LastGoal;
    public (uint Id, float X, float Y, float Z)? LastTarget;
    public (uint Id, float X, float Y, float Z)? LastFacing;
    public uint? Stopped;
    public (uint Id, bool Collidable)? LastNav;
    public void SetLocomotionGoal(uint id, float x, float y, float z, float stop) => LastGoal = (id, x, y, z, stop);
    public void SetLocomotionTarget(uint id, float x, float y, float z) => LastTarget = (id, x, y, z);
    public void SetFacing(uint id, float x, float y, float z) => LastFacing = (id, x, y, z);
    public void StopLocomotion(uint id) => Stopped = id;
    public void SetNavCollision(uint id, bool collidable) => LastNav = (id, collidable);
    public float GetModifiedMoveSpeed(uint id) => id == 10 ? 7.5f : 0f;
    public bool TryGetGoalDistance(uint id, out float d) { d = id == 10 ? 20f : 0f; return id == 10; }
```

Add a compile-anchor test (the real behavior tests live in Tasks 5-7):

```csharp
[Fact]
public void BridgeExposesLocomotionApi()
{
    var b = new FakeBridge();
    b.SetLocomotionGoal(10, 1, 2, 3, 0.5f);
    Assert.Equal((10u, 1f, 2f, 3f, 0.5f), b.LastGoal);
    Assert.Equal(7.5f, b.GetModifiedMoveSpeed(10));
}
```

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test … --filter BridgeExposesLocomotionApi`. Expected: FAIL (interface members / FakeBridge members not yet present → compile error until interface is added). (You are adding them to `FakeBridge` in Step 1 and the interface in Step 3; the RED here is the compile failure from `GameScriptContext`/`MutableHpBridge` not yet implementing the new interface members — proceed to Step 3.)

- [ ] **Step 3: Add interface + impls** —

In `ScriptContextRegistry.cs`, add to `IScriptGameBridge` the 7 members listed in **Interfaces** above.

In `GameScriptContext.cs`, implement them (place after `SetVisible`):

```csharp
public void SetLocomotionGoal(uint objectId, float x, float y, float z, float stopDistance)
{
    if (!_game.Objects.Objects.TryGetValue(objectId, out var o)) return;
    // Mirror C++ Locomotion::SetGoalPosition (GoalFlags=0x001, clears stale target/facing).
    o.GoalPosition = new System.Numerics.Vector3(x, y, z);
    o.TargetPosition = System.Numerics.Vector3.Zero;
    o.Facing = System.Numerics.Vector3.Zero;
    o.GoalFlags = 0x001;
    o.AllowedStopDistance = 0f;
    o.DirtyFlags |= ObjectDirtyFlags.Locomotion;
    // stopDistance retained server-side for the arrival estimate only (0x95 carries goal, not stop dist).
    o.DesiredStopDistance = stopDistance;
}

public void SetLocomotionTarget(uint objectId, float x, float y, float z)
{
    if (!_game.Objects.Objects.TryGetValue(objectId, out var o)) return;
    o.TargetPosition = new System.Numerics.Vector3(x, y, z);
    o.DirtyFlags |= ObjectDirtyFlags.Locomotion;
}

public void SetFacing(uint objectId, float x, float y, float z)
{
    // Server-side only in Wave 2 (visible turn-in-place deferred — see plan).
    if (_game.Objects.Objects.TryGetValue(objectId, out var o))
        o.Facing = new System.Numerics.Vector3(x, y, z);
}

public void StopLocomotion(uint objectId)
{
    if (!_game.Objects.Objects.TryGetValue(objectId, out var o)) return;
    o.TargetPosition = System.Numerics.Vector3.Zero;
    o.Facing = System.Numerics.Vector3.Zero;
    o.GoalFlags = 0x020;
    o.DirtyFlags |= ObjectDirtyFlags.Locomotion;
}

public void SetNavCollision(uint objectId, bool collidable)
{
    // Client SetNavCollision @0x009fe7c0 writes the INVERTED collidable flag; server-side only, no wire.
    if (_game.Objects.Objects.TryGetValue(objectId, out var o))
        o.NavCollisionDisabled = !collidable;
}

public float GetModifiedMoveSpeed(uint objectId) =>
    _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.MoveSpeed : 0f;

public bool TryGetGoalDistance(uint objectId, out float distance)
{
    if (_game.Objects.Objects.TryGetValue(objectId, out var o))
    {
        distance = System.Numerics.Vector3.Distance(o.Position, o.GoalPosition);
        return true;
    }
    distance = 0f;
    return false;
}
```

Wait — `GameObject` has no `AllowedStopDistance`/`DesiredStopDistance` (those are on `LocomotionData`, not `GameObject`). Remove the two lines referencing them and store the stop distance in a new `GameObject.DesiredStopDistance` field instead: add `public float DesiredStopDistance { get; set; }` to `GameObject` (Task 2 also touches this file — if not added there, add it here). Adjust the two lines to `o.DesiredStopDistance = stopDistance;` and drop the `AllowedStopDistance` line.

In `SchedulerPredicateTests.cs`, add the same 7 members to `MutableHpBridge` (benign defaults): `SetLocomotionGoal`/`SetLocomotionTarget`/`SetFacing`/`StopLocomotion`/`SetNavCollision` empty bodies; `GetModifiedMoveSpeed(uint) => 5f;` `TryGetGoalDistance(uint, out float d) { d = 0f; return false; }`.

- [ ] **Step 4: Run test + build, verify pass** — Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "BridgeExposesLocomotionApi|Scripting"` (ignore only the known pre-existing `EnemyNounCombatDataTests` failure). Expected: build clean, all green except that one.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs ReCap.Server/Services/Scripting/GameScriptContext.cs ReCap.Server/Domain/Gameplay/ObjectManager.cs ReCap.Tests/Scripting/GameBridgeTests.cs ReCap.Tests/Scripting/SchedulerPredicateTests.cs
git commit -m "feat(lua): IScriptGameBridge locomotion API + GameScriptContext impl"
```

---

### Task 5: NLocomotionModule (Stop, SlideToPoint, MoveToCircleEdge, TurnToFace)

**Files:** Create `ReCap.Server/Adapters/Scripting/Api/NLocomotionModule.cs`; Modify `LuaRuntime.cs:53-70`; Test `GameBridgeTests.cs`.

**Interfaces:** Produces Lua globals: `nLocomotion.Stop(objId)` → 0; `nLocomotion.SlideToPoint(objId,x,y,z,speed)` → 0; `nLocomotion.MoveToCircleEdge(objId,x,y,z,radius,[faceGoal])` → 1 bool; `nLocomotion.TurnToFace(objId,x,y,z,[immediate])` → 0.
Contracts: Ghidra `nLocomotion::SlideToPoint@0x00a046b0`, `MoveToCircleEdge@0x00a049c0` (stop-dist=radius, returns had-locomotion bool), `TurnToFace@0x00a05300` (SetFacing path), `Stop@0x00a1a150`.

- [ ] **Step 1: Write failing tests** — in `GameBridgeTests.cs`:

```csharp
[Fact]
public void LocomotionNativesDriveBridge()
{
    using var rt = Make();
    var b = (FakeBridge)ScriptContextRegistry.Get(rt.L)!.GameBridge!;
    rt.Execute(LuaFixtures.Compile("""
        nLocomotion.SlideToPoint(10, 1, 2, 3, 6)
        nLocomotion.MoveToCircleEdge(10, 4, 5, 6, 2.5, true)
        nLocomotion.TurnToFace(10, 7, 8, 9)
        nLocomotion.Stop(10)
        """), "loco");
    Assert.Equal((10u, 4f, 5f, 6f, 2.5f), b.LastGoal); // MoveToCircleEdge is the last goal set
    Assert.Equal((10u, 7f, 8f, 9f), b.LastFacing);
    Assert.Equal(10u, b.Stopped);
}

[Fact]
public void MoveToCircleEdgeReturnsBool()
{
    using var rt = Make();
    Assert.True(rt.EvalBool(LuaFixtures.Compile(
        "return nLocomotion.MoveToCircleEdge(10, 0, 0, 0, 1) == true")));
}
```

- [ ] **Step 2: Run tests, verify they fail** — Run: `dotnet test … --filter "LocomotionNativesDriveBridge|MoveToCircleEdgeReturnsBool"`. Expected: FAIL (nLocomotion is a stub → no bridge calls / returns nothing).

- [ ] **Step 3: Create the module** — `NLocomotionModule.cs`:

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NLocomotionModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nLocomotion",
            ("Stop", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&Stop),
            ("SlideToPoint", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SlideToPoint),
            ("MoveToCircleEdge", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MoveToCircleEdge),
            ("TurnToFace", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&TurnToFace));

    private static bool Num(nint L, int i, out float v)
    {
        if (LuaNative.lua_type(L, i) == LuaNative.LUA_TNUMBER) { v = (float)LuaNative.lua_tonumber(L, i); return true; }
        v = 0f; return false;
    }

    // Ghidra nLocomotion::SlideToPoint@0x00a046b0: (objId,x,y,z,speed) -> sets positional goal + speed.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SlideToPoint(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && Num(L, 1, out var id) && Num(L, 2, out var x) && Num(L, 3, out var y) && Num(L, 4, out var z))
                bridge.SetLocomotionGoal((uint)Math.Round(id), x, y, z, 0f);
        }
        catch { }
        return 0;
    }

    // Ghidra nLocomotion::MoveToCircleEdge@0x00a049c0: (objId,x,y,z,radius,[faceGoal]) -> goal with
    // stop-distance=radius; returns 1 bool (had-locomotion). faceGoal flag deferred (0x800 face-on-arrival).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int MoveToCircleEdge(nint L)
    {
        var ok = false;
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && Num(L, 1, out var id) && Num(L, 2, out var x) && Num(L, 3, out var y) && Num(L, 4, out var z) && Num(L, 5, out var radius))
            {
                bridge.SetLocomotionGoal((uint)Math.Round(id), x, y, z, radius);
                ok = true;
            }
        }
        catch { }
        LuaNative.lua_pushboolean(L, ok ? 1 : 0);
        return 1;
    }

    // Ghidra nLocomotion::TurnToFace@0x00a05300: (objId,x,y,z,[immediate]) -> SetFacing path.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int TurnToFace(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && Num(L, 1, out var id) && Num(L, 2, out var x) && Num(L, 3, out var y) && Num(L, 4, out var z))
                bridge.SetFacing((uint)Math.Round(id), x, y, z);
        }
        catch { }
        return 0;
    }

    // Ghidra Locomotion::Stop@0x00a1a150: (objId) -> GoalFlags=0x020.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Stop(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && Num(L, 1, out var id))
                bridge.StopLocomotion((uint)Math.Round(id));
        }
        catch { }
        return 0;
    }
}
```

- [ ] **Step 4: Wire registration** — in `LuaRuntime.cs`, after `Api.NThreadDataModule.Register(L);` add:

```csharp
        Api.NLocomotionModule.Register(L);
```

- [ ] **Step 5: Run tests, verify pass** — Run: `dotnet test … --filter "LocomotionNativesDriveBridge|MoveToCircleEdgeReturnsBool|Scripting"` (ignore only the known pre-existing failure). Expected: green.

- [ ] **Step 6: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/Api/NLocomotionModule.cs ReCap.Server/Adapters/Scripting/LuaRuntime.cs ReCap.Tests/Scripting/GameBridgeTests.cs
git commit -m "feat(lua): nLocomotion module (Stop/SlideToPoint/MoveToCircleEdge/TurnToFace)"
```

---

### Task 6: nGameObject.SetTargetPosition + SetNavCollision + GetModifiedMoveSpeed

**Files:** Modify `NGameObjectModule.cs`; Test `GameBridgeTests.cs`.

**Interfaces:** Produces `nGameObject.SetTargetPosition(objId,x,y,z)` → 0; `nGameObject.SetNavCollision(objId,collidable)` → 0; `nGameObject.GetModifiedMoveSpeed(objId)` → 1 number.
Contracts: catalog §Locomotion; Ghidra `SetNavCollision@0x009fe7c0` (inverted flag, server-side).

- [ ] **Step 1: Write failing tests** — in `GameBridgeTests.cs`:

```csharp
[Fact]
public void GameObjectLocomotionNativesDriveBridge()
{
    using var rt = Make();
    var b = (FakeBridge)ScriptContextRegistry.Get(rt.L)!.GameBridge!;
    Assert.True(rt.EvalBool(LuaFixtures.Compile("""
        nGameObject.SetTargetPosition(10, 3, 4, 5)
        nGameObject.SetNavCollision(10, false)
        return nGameObject.GetModifiedMoveSpeed(10) == 7.5 and nGameObject.GetModifiedMoveSpeed(99) == 0
        """)));
    Assert.Equal((10u, 3f, 4f, 5f), b.LastTarget);
    Assert.Equal((10u, false), b.LastNav);
}
```

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test … --filter GameObjectLocomotionNativesDriveBridge`. Expected: FAIL (stubs).

- [ ] **Step 3: Implement** — add to `NGameObjectModule.cs` Register list and methods (follow the file's existing entry style):

```csharp
("SetTargetPosition", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetTargetPosition),
("SetNavCollision", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetNavCollision),
("GetModifiedMoveSpeed", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetModifiedMoveSpeed),
```

```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int SetTargetPosition(nint L)
{
    try
    {
        var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
        if (bridge is not null
            && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
            && LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER
            && LuaNative.lua_type(L, 3) == LuaNative.LUA_TNUMBER
            && LuaNative.lua_type(L, 4) == LuaNative.LUA_TNUMBER)
        {
            bridge.SetLocomotionTarget(
                (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)),
                (float)LuaNative.lua_tonumber(L, 2), (float)LuaNative.lua_tonumber(L, 3), (float)LuaNative.lua_tonumber(L, 4));
        }
    }
    catch { }
    return 0;
}

// Ghidra nGameObject::SetNavCollision@0x009fe7c0: server-side pathfinding flag (inverted), no wire.
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int SetNavCollision(nint L)
{
    try
    {
        var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
        if (bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
        {
            var collidable = LuaNative.lua_toboolean(L, 2) != 0;
            bridge.SetNavCollision((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)), collidable);
        }
    }
    catch { }
    return 0;
}

[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int GetModifiedMoveSpeed(nint L)
{
    try
    {
        var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
        var speed = bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
            ? bridge.GetModifiedMoveSpeed((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)))
            : 0f;
        LuaNative.lua_pushnumber(L, speed);
        return 1;
    }
    catch
    {
        LuaNative.lua_pushnumber(L, 0f);
        return 1;
    }
}
```

Note: verify `LuaNative.lua_toboolean` exists with signature `(nint, int) -> int`; if the exact name differs, use the same boolean-arg reader other natives use (grep `lua_toboolean`/bool-arg helper in `Api/`). If none exists, read arg2 as a number and treat `!= 0` as true.

- [ ] **Step 4: Run tests, verify pass** — Run: `dotnet test … --filter "GameObjectLocomotionNativesDriveBridge|Scripting"` (ignore the known pre-existing failure). Expected: green.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs ReCap.Tests/Scripting/GameBridgeTests.cs
git commit -m "feat(lua): nGameObject.SetTargetPosition + SetNavCollision + GetModifiedMoveSpeed"
```

---

### Task 7: nThread.WaitForNearGoal + WaitForJumpComplete (time-estimate)

**Files:** Modify `NThreadModule.cs`; Test `SchedulerPredicateTests.cs`.

**Interfaces:** Consumes `IScriptGameBridge.TryGetGoalDistance`, `GetModifiedMoveSpeed`, `Scheduler.RegisterYield`.
Produces `nThread.WaitForNearGoal(objId, distThreshold, a3, a4, a5)` and `nThread.WaitForJumpComplete(objId)`, both → yield, 0 returns.
Because there is no server-side movement integration, both resume on a **time estimate** (not a live predicate): WaitForNearGoal wakes after `max(0, goalDistance - distThreshold) / max(moveSpeed, 0.01)` seconds, capped by the timeout arg4 (default 10); WaitForJumpComplete wakes after a `DEFERRED:` default jump duration. Cite this approximation.

- [ ] **Step 1: Write failing test** — in `SchedulerPredicateTests.cs` (reuse a bridge fake; extend `MutableHpBridge` with `TryGetGoalDistance` returning a set distance and `GetModifiedMoveSpeed => 10f` if not already added in Task 4):

```csharp
[Fact]
public void WaitForNearGoalResumesAfterEstimatedTravelTime()
{
    using var rt = LuaRuntime.CreateSandboxedState();
    var ctx = ScriptContextRegistry.Get(rt.L)!;
    ctx.GameBridge = new MutableHpBridge(); // goalDistance 20, moveSpeed 10 → ~2s travel
    var scheduler = ctx.Scheduler!;
    rt.Execute(LuaFixtures.Compile("""
        nThread.CreateThreadForObject(10, function()
            nThread.WaitForNearGoal(10, 0, -1, 10, true)
        end)
        """), "wng");

    scheduler.Tick(1.0);
    Assert.True(scheduler.HasThreadForObject(10));  // ~2s estimate not elapsed
    scheduler.Tick(3.5);
    Assert.False(scheduler.HasThreadForObject(10)); // elapsed → resumed
}
```

Ensure `MutableHpBridge.TryGetGoalDistance(uint, out float d)` returns `d=20f; return true;` and `GetModifiedMoveSpeed(uint) => 10f;`.

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test … --filter WaitForNearGoalResumesAfterEstimatedTravelTime`. Expected: FAIL (stub → coroutine finishes immediately → not parked).

- [ ] **Step 3: Implement** — add to `NThreadModule.cs` Register list + methods:

```csharp
("WaitForNearGoal", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitForNearGoal),
("WaitForJumpComplete", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitForJumpComplete),
```

```csharp
// DEFERRED (no server-side movement integration): resume on a time estimate, not a live position
// predicate. WaitForNearGoal args 3-5 (-1,10,true) roles unconfirmed; arg4 treated as timeout cap.
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int WaitForNearGoal(nint L)
{
    try
    {
        var ctx = ScriptContextRegistry.Get(L);
        var scheduler = ctx?.Scheduler;
        var bridge = ctx?.GameBridge;
        if (scheduler is not null && bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
        {
            var objId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var threshold = LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER ? (float)LuaNative.lua_tonumber(L, 2) : 0f;
            var timeout = LuaNative.lua_type(L, 4) == LuaNative.LUA_TNUMBER ? (double)LuaNative.lua_tonumber(L, 4) : 10.0;
            var remaining = bridge.TryGetGoalDistance(objId, out var dist) ? Math.Max(0f, dist - threshold) : 0f;
            var speed = Math.Max(0.01f, bridge.GetModifiedMoveSpeed(objId));
            var estimate = Math.Min(remaining / speed, timeout);
            scheduler.RegisterYield(L, sleeping: false, wakeAt: scheduler.Now + estimate);
        }
        else return 0;
    }
    catch (Exception ex)
    {
        try { Util.Logging.Log.Lua.Error($"[nThread] WaitForNearGoal failed: {ex.Message}"); } catch { }
        return 0;
    }
    return LuaNative.lua_yield(L, 0);
}

// DEFERRED default jump duration (real source: jump anim/locomotion tuning, not yet parsed).
private const double DefaultJumpDurationSeconds = 0.6;

[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int WaitForJumpComplete(nint L)
{
    try
    {
        var scheduler = ScriptContextRegistry.Get(L)?.Scheduler;
        scheduler?.RegisterYield(L, sleeping: false, wakeAt: scheduler.Now + DefaultJumpDurationSeconds);
    }
    catch (Exception ex)
    {
        try { Util.Logging.Log.Lua.Error($"[nThread] WaitForJumpComplete failed: {ex.Message}"); } catch { }
        return 0;
    }
    return LuaNative.lua_yield(L, 0);
}
```

- [ ] **Step 4: Run test, verify pass** — Run: `dotnet test … --filter "WaitForNearGoalResumesAfterEstimatedTravelTime|Scripting"` (ignore the known pre-existing failure). Expected: green.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/Api/NThreadModule.cs ReCap.Tests/Scripting/SchedulerPredicateTests.cs
git commit -m "feat(lua): nThread.WaitForNearGoal + WaitForJumpComplete (time-estimate)"
```

---

### Task 8: Ratchet + full suite + gate note

**Files:** none (verification); update spec/memory.

- [ ] **Step 1: Full suite** — Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj`. Expected: all green except the known pre-existing `EnemyNounCombatDataTests` failure.
- [ ] **Step 2: Harvest ratchet** — Run: `RECAP_HARVEST=1 dotnet test ReCap.Tests/ReCap.Tests.csproj --filter NativeDemandHarvest --logger "trx;LogFileName=h.trx"`; extract demanded list from the TRX `<StdOut>`. Expected: demanded dropped by the 9 Wave-2 natives (18 → ~9). Remaining should be Wave 3 (modifier/FX) + Wave 4 (spawn).
- [ ] **Step 3: Record** — update the catalog spec's harvest number and note Wave 2 complete + the deferred items (visible turn-in-place, per-noun move speed / jump duration, real arrival detection) in `memory/lua-system-contract.md`. Commit:

```bash
git add docs/superpowers/specs/2026-07-08-lua-demanded-natives-catalog-design.md
git commit -m "docs: Wave 2 locomotion natives landed; update demanded count"
```

**In-game gate (manual, after merge):** cast a charge/movement ability at range in a dungeon; expect the NPC/agent to slide toward the goal (0x95 broadcast) rather than teleport. If it still teleports, confirm `GoalFlags & 0x001` is set (goal path) not `0x020` (stop path).

## Self-Review

- **Spec coverage:** the 9 Wave-2 natives map to Tasks 5-7; the 0x95 wire + broadcast + fields + bridge that they depend on are Tasks 1-4; ratchet gate is Task 8. Deferred items (visible facing, per-noun speed/jump duration, real arrival detection) are stated in Scope and cited in code. ✓
- **Placeholder scan:** every code step has complete code; the two data-derived values (MoveSpeed default, jump duration) are named constants with `DEFERRED:` cites per the global constraint, not bare literals; Task 3's testing limitation and Task 6's `lua_toboolean` uncertainty carry explicit fallback instructions. ✓
- **Type consistency:** `IScriptGameBridge`'s 7 new members (Task 4) are the exact signatures consumed by Tasks 5-7 and implemented in `GameScriptContext`/`FakeBridge`/`MutableHpBridge`; `GameObject.DesiredStopDistance` is added where first referenced; `LocomotionDataUnreliableUpdatePacket.GoalPosition` (Task 1) matches the `FlushObjectUpdates` construction (Task 3). ✓

**Impl-time note:** `GameObject` (in `ObjectManager.cs`) and `IScriptGameBridge` are touched by multiple tasks — after each interface change, build all three bridge implementers so the fakes stay in sync, and re-run the full suite before the ratchet.
