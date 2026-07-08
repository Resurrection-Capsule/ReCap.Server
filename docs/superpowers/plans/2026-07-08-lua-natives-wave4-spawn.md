# Lua Natives — Wave 4 (Spawn) Implementation Plan

> **For agentic workers:** executed INLINE (2 natives) with a final whole-branch review, not subagent-per-task. Steps use `- [ ]` for tracking.

**Goal:** Implement the last 2 demanded Lua natives — `nObjectManager.CreateObject` and `nObjectManager.AttachTriggerVolume` — closing the 27-native harvest set.

**Architecture:** `CreateObject` allocates an object id, spawns a server-side `GameObject` via `ObjectManager.Spawn`, and announces it via the existing **0x8C ObjectCreate** packet (client `OnGmsObjectCreate@0x0053f550` — a reflection envelope that tolerates optional fields, verified this session) using the wire-verified enemy-shape field set `{6,7}`. The announce is **gated on the noun resolving server-side** (if `AssetDatabase` resolves it the client can too; an unresolvable noun spawns server-side only, invisible, to avoid a client noun-load crash). `AttachTriggerVolume` registers a sphere trigger + captures its optional Lua callback closures (`luaL_ref`); trigger **firing is deferred** (no server-side collision system).

**Tech Stack:** C# net10.0, native lua 5.1.4 P/Invoke, xUnit.

## Global Constraints

- net10.0; native `LUA_NUMBER = float`; pushed numbers `(float)`. Every native exception-proof, returns its declared count on all paths.
- **0x8C ObjectCreate** = `objId + GameObjectCreateData(reflection) + SporelabsObject(reflection)`; reflection = optional fields (client tolerates missing). Reuse the existing `ObjectCreatePacket` with the enemy-shape SporelabsObject bit set `{6,7}` (wire-verified, `SpawnWorldObject` enemy path). Do not invent fields.
- Announce only when `ObjectManager.NounResolves(nounId)` is true (crash-safety gate).
- No hardcoded data-derived values without a `DEFERRED:` cite. No AI attribution. Comments only short cites.

## Scope

**In (2 natives):** nObjectManager.CreateObject, nObjectManager.AttachTriggerVolume; a bridge `CreateObject`, `Game.SpawnScriptObject`, `ObjectManager.NounResolves`, a `ScriptStateContext` trigger store.

**Deferred (flagged):** CreateObject orientation/scale/owner args (callers chain SetTeam/SetAttributeSnapshot/SetTargetPosition); projectile physics/homing (no movement sim); visible spawn for unresolvable-noun handles (GetAsset handle vs wire-noun domain — announce gated on resolution, likely invisible until domains reconciled); trigger-volume FIRING (onEnter/exit/stay — no collision system; callback refs captured for a future firing pass, freed by `lua_close` on game teardown).

## Tasks (inline)

### Task 1: nObjectManager.CreateObject

- [ ] **1a. Failing test** — `GameBridgeTests.cs`: extend `FakeBridge` with a `CreateObject` recorder returning an incrementing id, then:
```csharp
[Fact]
public void CreateObjectSpawnsAndReturnsId()
{
    using var rt = Make();
    var b = (FakeBridge)ScriptContextRegistry.Get(rt.L)!.GameBridge!;
    Assert.True(rt.EvalBool(LuaFixtures.Compile(
        "local id = nObjectManager.CreateObject(4660, 1, 2, 3) return id > 0")));
    Assert.Single(b.CreatedObjects);
    Assert.Equal(4660u, b.CreatedObjects[0].Noun);
}
```
FakeBridge:
```csharp
public List<(uint Noun, float X, float Y, float Z)> CreatedObjects { get; } = [];
private uint _nextCreatedId;
public uint CreateObject(uint nounId, float x, float y, float z) { CreatedObjects.Add((nounId, x, y, z)); return ++_nextCreatedId; }
```
- [ ] **1b. Run → fail** (interface/native missing).
- [ ] **1c. Implement:**
  - `IScriptGameBridge`: `uint CreateObject(uint nounId, float x, float y, float z);`
  - `ObjectManager`: `public bool NounResolves(uint nounId) => _db?.GetNoun(nounId) is not null;`
  - `Game`: `SpawnScriptObject(uint nounId, Vector3 position)` → `_nextObjectId++`, `Objects.Spawn(id, nounId, position, 1f, team:0, playerControlled:false)`, and if `Objects.NounResolves(nounId)` broadcast `ObjectCreatePacket` (CreateData `{Noun, Position, Scale=1, Team=0, HasCollision=false, PlayerControlled=false}`, SporelabsObject `{Position, Orientation=Identity}` + `SetDataBit(6); SetDataBit(7);`) via `BroadcastToAllPlayers`; return id.
  - `GameScriptContext`: `public uint CreateObject(uint nounId, float x, float y, float z) => _game.SpawnScriptObject(nounId, new Vector3(x, y, z));`
  - `MutableHpBridge`: `public uint CreateObject(uint n, float x, float y, float z) => 0;`
  - `NObjectManagerModule`: add `("CreateObject", …)` + a native reading noun(arg1)+x/y/z(arg2-4), delegating to bridge, pushing the id (0f fallback on all failure paths).
- [ ] **1d. Run → pass**; **1e. commit** `feat(lua): nObjectManager.CreateObject (spawn + gated 0x8C announce)`.

### Task 2: nObjectManager.AttachTriggerVolume

- [ ] **2a. Failing test** — `GameBridgeTests.cs`:
```csharp
[Fact]
public void AttachTriggerVolumeRegistersAndReturnsHandle()
{
    using var rt = Make();
    var ctx = ScriptContextRegistry.Get(rt.L)!;
    Assert.True(rt.EvalBool(LuaFixtures.Compile(
        "local h = nObjectManager.AttachTriggerVolume(10, 5.0, function() end) return h > 0")));
    Assert.Equal(1, ctx.TriggerVolumeCount);
}
```
- [ ] **2b. Run → fail.**
- [ ] **2c. Implement:**
  - `ScriptStateContext`: a trigger store — `RegisterTriggerVolume(uint objectId, float radius, int[] callbackRefs) → uint handle` (incrementing) + `public int TriggerVolumeCount => _triggerVolumes.Count`.
  - `NObjectManagerModule`: add `("AttachTriggerVolume", …)` + a native reading objId(arg1)+radius(arg2), capturing args 3-5 that are `LUA_TFUNCTION` via `lua_pushvalue`+`luaL_ref`, calling `ctx.RegisterTriggerVolume`, pushing the handle (0f fallback). Cite: sphere-only, firing deferred (Ghidra `@0x00a07e00`).
- [ ] **2d. Run → pass**; **2e. commit** `feat(lua): nObjectManager.AttachTriggerVolume (sphere + captured Lua callbacks; firing deferred)`.

### Task 3: Ratchet + full suite + final review

- [ ] Full suite green (except pre-existing `EnemyNounCombatDataTests`). Harvest: demanded **2 → 0** (all 27 done). Update spec + memory; commit. Dispatch final whole-branch review (opus) over the Wave 4 diff.

**In-game gate (manual):** an ability whose tick calls `CreateObject` with a resolvable projectile/pet noun spawns a visible object (0x8C); unresolvable nouns spawn invisibly (server-side only) — expected until the GetAsset/wire-noun domain is reconciled.
