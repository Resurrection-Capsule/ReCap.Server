# AI Director — Faithful Vertical Slice (one enemy) — Design

**2026-07-09.** Make one pre-spawned enemy aggro, pursue, and attack the hero — driven faithfully by
its `AIDefinition` gambit graph and the Ghidra-verified aggro/perception model — reusing the
ability-cast, locomotion, and CombatEvent paths already built. First slice of the faithful
behavior-graph AI port (nAgent / nBehaviorTree / nGameDirector). One vertical slice touching the
minimum of all three subsystems to prove the pipeline end-to-end.

## Goal & non-goals

**Goal:** a basic Dungeon minion, when the hero enters its perception, aggros → pursues (locomotion)
→ casts its basic ability (real FX + damage + CombatEvent numbers) → on death runs its deathAbility
and despawns. Behavior selection comes from the enemy's own `AIDefinition`, not a hardcoded loop.

**Non-goals (deferred sub-projects):** nGameDirector spawn orchestration (horde/minion/lieutenant
waves + DirectorState-driven spawning); node/condition/gambit types beyond what the chosen slice
enemy uses; multi-enemy threat balancing; AgentBlackboardUpdate (0x99) wire broadcast.

## Verified contract (Ghidra + AssetData.Parser, 2026-07-09)

**Aggro / perception data model (Ghidra):**
- `GameObject + 0x2b0` = **cAgentBlackboard** sub-object (0 ⇒ not an AI agent; gates every nAgent fn).
  Registrar `AssetData::cAgentBlackboard` @`0x00f737b0`.
- Aggro list at blackboard `+0x228` (begin) / `+0x22c` (end): vector of 8-byte entries, entry[0] =
  target objId; count = `(end-begin)>>3`. Ordered/pre-prioritized, not a per-tick nearest-scan.
- Aggro API natives: `AddAggroForObject` @`0x009fd690` (populate), `GetAggroForObject` @`0x009fd700`
  (threat), `OverrideFirstAggro`/`IgnoreFirstAggro`, `Set/GetAggroDelegate`; aggro anim transitions
  `SetAnimationStateToPreAggroIdle` @`0x009fc790` → `SetAnimationStateToAggro` @`0x009fc4e0`.
- Perception radius is **per-object** (`FUN_009e8fc0`: obj+4→+0x80→max(+0x38,+0x3c); default
  `_DAT_00fdc000`). `nAgent::InPerceptionCircle` @`0x00a059a0` = point within `radius + offset` of obj.

**nAgent (4 natives @`0x00a05a90`):** `GetTargetsOnAggroList` (table of ids), `HasTargetsOnAggroList`,
`GetBestTarget` (= **first valid** entry — alive/targetable/hostile — via
`nAgent::SelectFirstValidAggroTarget` @`0x009e8dd0`), `InPerceptionCircle`.

**nBehaviorTree (2 natives, inline):** `GetMyObjectID` @`0x009fad20`, `GetTargetObjectID` @`0x009fad60`.
Both read the per-object **`Simulation::GetThreadContext`** (@`0x008f3d00`) "self" (obj+0x10 = id) —
the same context stack abilities run in.

**AIDefinition (AssetData, loaded already by `AssetDatabase.GetAIDefinition`):**
- `AIDefinition` = `ainode[]` (`cAINode`) + `deathAbility`(Key).
- `cAINode` = `mpPhaseData`(Asset) + `mpConditionData`(Asset) + `nodeX/nodeY`(editor) + `output[]`(Int
  edges) → a **behavior graph / state machine**: condition data decides transitions, phase data is
  the action.
- `cGambitDefinition` = `condition`(Key)+`conditionProps` → `ability`(Key)+`abilityProps` +
  `randomizeCooldown`(Bool) — an **IF condition THEN ability** rule (registrar @`0x00f94420`).
- `cAICondition` = `conditionType` + `namespace` + `name` + `properties` — a `namespace::name`
  predicate evaluated by `nCondition`.

**Execution model (Ghidra):** AI behavior executes **per-object in the same `Simulation::GetThreadContext`
tick as abilities**. ⇒ the AIController reuses the existing `ScriptContext` ability-tick / thread-context
infrastructure rather than a new engine.

## Architecture / components

1. **`AgentBlackboard`** (C# domain, hangs off `GameObject`) — mirrors `cAgentBlackboard`: ordered
   aggro list `{objId, threat}`, per-object perception radius, targetable/alive flags. Present only on
   AI-agent objects (non-player nouns whose AIDefinition resolves); absence = inert (the obj+0x2b0==0 gate).

2. **`AggroSystem`** — ticked from `ObjectManager.Update`: for each agent, perceive hostile
   player-controlled objects (`InPerceptionCircle` about the agent) → `AddAggroForObject`; prune dead /
   far targets; drive the pre-aggro-idle → aggro animation transition on first contact. `GetBestTarget`
   = first valid on the list.

3. **`nAgent` / `nBehaviorTree` / `nCondition` natives** — thin C# natives over `AgentBlackboard` +
   the thread context, matching the Ghidra signatures. Registered into the existing Lua namespaces
   (currently empty stubs).

4. **`AIController`** — per agent, walks its `AIDefinition` `ainode` graph each behavior tick: evaluate
   the current node's condition data (`nCondition`) → transition along `output` edges; run the node's
   phase gambits (condition→ability): cast the first ready gambit's ability via the existing
   `InvokeAbility` at `GetBestTarget` (respecting cooldown); a pursue phase sets the locomotion goal
   toward the target. Runs inside the ability/behavior thread context so `GetMyObjectID`/`GetTargetObjectID`
   resolve.

5. **Integration (reuse, no new wire work):** attack = `InvokeAbility` → FX (0x9B) + damage + CombatEvent
   (0xBA); move = locomotion goal → 0x95; death = D-025 ObjectDelete + `deathAbility`.

## Data flow

spawn (enemy noun with resolved `AIDefinition` → `AgentBlackboard` attached) → each game tick:
`AggroSystem` sees hero in perception → aggro list populated → `AIController` (self=enemy,
target=best): evaluate gambit conditions → set locomotion goal toward hero until in ability range →
`InvokeAbility(basic)` → CombatEvent damage numbers → hero HP falls; on enemy HP≤0 → `deathAbility` +
ObjectDelete.

## Harvest (needs game data — runs on the user's machine, not this sandbox)

Pick one basic Dungeon minion noun. Dump its `AIDefinition` (the `ainode` graph + each node's
gambits + the `cAICondition` `namespace::name`s it references) and confirm the aggro-population
trigger (`AddAggroForObject` call sites: perceive vs. on-damage). Implement exactly the node / phase /
condition / gambit types that enemy uses — the harvest method proven for combat (31 natives demanded
by 382 tick-scripts), not the whole graph vocabulary up front. `log()` anything dropped.

## Error handling / edge cases

- No `AIDefinition` or no `AgentBlackboard` → not an agent → skipped (matches the obj+0x2b0==0 gate).
- Target dies / leaves perception → `GetBestTarget` returns the next valid id or 0 → AIController idles
  (returns to pre-aggro-idle).
- Ability on cooldown / insufficient mana → that gambit is skipped, next gambit evaluated.
- Concurrency: AI runs on the game-loop thread; `ObjectManager` already uses `ConcurrentDictionary`
  (spawns/removes arrive on the RakNet thread) — the AI tick only reads/writes agent state on the loop thread.
- Unknown condition `namespace::name` (outside the harvested set) → treated as false + logged for the
  next harvest pass (never throws mid-tick).

## Testing

- **Unit:** `AgentBlackboard` add/prune + `GetBestTarget` = first valid; `InPerceptionCircle` math;
  `AIController` node transition (condition→edge) and gambit selection (condition→ability) against a
  fake bridge; cooldown-skip path.
- **Gate (in-game, user machine):** enemy aggros the hero on approach, pursues, casts its ability,
  deals damage (CombatEvent numbers appear), and dies to its deathAbility.

## Open items resolved to defaults

- Perception radius source: use the per-object value the asset provides (ClassAttributes/noun); until
  the exact field is harvested, a documented default constant (logged), replaced during harvest.
- Best-target rule: first-valid-on-list (Ghidra-confirmed), not nearest — the aggro list ordering is
  the priority.
