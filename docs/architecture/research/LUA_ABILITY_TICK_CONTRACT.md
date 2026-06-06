# Lua Ability Tick Contract

Investigated 2026-06-06. Evidence basis: luac -l -l disassembly of retail Darkspore
ServerData.package Lua chunks (group 0x7153BBB1 Abilities, group 0x3681D755 Lua global).

---

## OOP Class Vtable Pattern

All registered abilities use a Class OOP system (global.lua, instance 0x57572DAC).
`Class.newClass()` builds tables where:
- Numeric key `2` = wrapper closure calling `self:tick()` via SELF opcode
- Numeric key `3` = wrapper closure calling `self:deactivate()` via SELF opcode
- String key `"tick"` accessible via `__index` metamethod on class tables

`TableHasFunction(L, tableIdx, "tick")` correctly detects tick presence via the metamethod.
Boot: 283 Abilities + 99 Modifiers registered HasTick=true.

---

## Samples

| Ability / Template | Source Chunk | tick numparams | Context Getters | Notes |
|---|---|---|---|---|
| template_ability_firstaggro.lua | 0x7153BBB1!0x78AA702F | **1** (self) | GetAgentID, GetAgentAttributeSnapshot | Sets visibility, plays anim |
| template_ability_projectile.lua | 0x7153BBB1!? | **9** | None | Reads args positionally |
| ability_SentryDroneLaser.lua | 0x7153BBB1!? | 0 (wrapper) → inherits template_projectile (9) | None | Wrapper SELF "tick" |
| ability_Fireball.lua | 0x7153BBB1!? | 0 (wrapper) → inherits template_projectile (9) | None | Wrapper SELF "tick" |
| ability_spawn.lua | 0x7153BBB1!0x4C96D649 | 0 (wrapper numeric slot) | GetAgentID, GetRank | Direct class body |

Note: "wrapper" closures at numeric slot 2 have 0 params and simply dispatch via SELF,
so the ACTUAL tick function's numparams is what matters (checked on the template).

---

## 9-Param Tick Layout (template_ability_projectile family)

`tick(self, agentId, targetId, p3, cursorX, cursorY, cursorZ, rank, p8)`

- Param 0 = self (the registered table)
- Param 1 = agentId (uint as float, passed directly to nGameObject calls)
- Param 2 = targetId (uint as float)
- Param 3 = unknown (not observed being used in early instructions)
- Params 4,5,6 = cursorX, cursorY, cursorZ
- Param 7 = rank (passed to GetRankedValueHelper)
- Param 8 = unknown

---

## Decision Rule (from plan; confirmed by evidence)

`numparams` is checked on the ACTUAL tick function (via `tbl.tick`, which resolves via __index):

- `numparams >= 4`: call `tick(selfTable, agentId, targetId, 0, cursorX, cursorY, cursorZ, rank, 0)`
  trimmed or padded to exactly numparams (pass only as many of the 9 as the function declares,
  or 0 for extras beyond 7 standard args).
- `numparams <= 1`: call `tick(selfTable)` — function uses context getters instead of positional args.
- `numparams = 0`: this is a wrapper dispatch closure; get the real tick via the class SELF chain
  (call `tbl[2](tbl)` or equivalently `tbl:tick()` via pcall).

---

## Implementation Method

`InvokeAbility` in `GameScriptContext`:
1. Look up entry in registry (HasTick required).
2. Guard `HasThreadForObject(agentId)`.
3. Push `entry.TableRef` table onto Lua stack.
4. `lua_getfield(L, -1, "tick")` to get the function.
5. `lua_insert` to put tick below the table (table becomes self-arg).
6. Push additional args per decision rule.
7. `SpawnFromStack` moves fn+args to a new coroutine thread.
8. `SetInvocation(threadL, invocation)` BEFORE first resume (via `beforeFirstResume` callback).

Context getters (`nAbility.GetAgentID()` etc.) read from the per-thread invocation slot.
This is correct for both 1-param (uses getters) and 9-param (reads positional params; getters
are redundant but harmless since they still return the same invocation data).
