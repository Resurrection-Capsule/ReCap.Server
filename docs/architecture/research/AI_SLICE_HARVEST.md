# AI Director Vertical Slice — Harvest (game data, 2026-07-09)

Harvested from the real Darkspore assets via `AssetData.Parser` CLI
(`AssetData_Binary.package`, 13800 entries, 136 registry types). This pins the slice enemy's
AIDefinition contract so Tasks 8–9 implement exactly what a basic minion needs.

## Slice enemy: `ZelemBasicMelee`

The simplest real basic-melee minion — chosen for the slice.

```
ZelemBasicMelee.AIDefinition:
  Struct AIDefinition
    Array ainode(1)
      Struct cAINode
        Asset  mpPhaseData -> ZelemBasicMelee.Phase   (asset REFERENCE, not inline)
        Int32  nodeX(0)  nodeY(0)
        Array  output(0)                               (no edges — single leaf node, no transitions)

ZelemBasicMelee.Phase:
  Struct Phase
    Array gambit(1)
      Struct cGambitDefinition
        Array conditionProps(0)                        (NO condition -> unconditional gambit)
        Asset ability -> ZelemBasicMeleeAttack
        Array abilityProps(0)
        Bool  randomizeCooldown(false)
    Enum phaseType(prioritizedList)
    Bool startNode(false)
```

**⇒ A basic melee minion = a single-node graph whose one Phase has one unconditional gambit that
casts its basic melee ability.** No conditions, no transitions. The pursue+strike is the *ability's*
job (ZelemBasicMeleeAttack's Lua tick drives locomotion + damage), not the AIController's. So the
slice needs **zero condition evaluators** — the AIController just: aggro → best target → cast the
(first/only) gambit's ability.

## Confirmed structural contract (drives Task 8)

- **`AIDefinition`** = `ainode[]` (`cAINode`) + `deathAbility`. Basic minions have **1 node**, `output(0)`.
- **`cAINode.mpPhaseData`** is an **Asset reference** (`<name>.Phase`) — the AIController must RESOLVE it
  (by name → `AssetDatabase.GetAssetByName`), not read it inline. (Task 6's synthetic walker read it
  inline; Task 8 adds the resolve step.)
- **`Phase`** = `gambit[]` (`cGambitDefinition`) + `phaseType` (**always `prioritizedList`** in every
  sample) + `startNode`(bool). prioritizedList = evaluate gambits in order, first whose condition
  passes wins (FF12-gambit model).
- **`cGambitDefinition`** = `condition` (Asset ref to a condition TYPE, may be ABSENT = unconditional)
  + `conditionProps` (`cAssetProperty[]` = the condition's parameters) + `ability` (Asset ref) +
  `abilityProps` + `randomizeCooldown`(bool).
- **`cAssetProperty`** = `key`(u32) + `name`(str) + `type`(u32) + `value`(str). Example (a Distance
  condition): `{name:"Distance", value:"10"}` + `{name:"GreaterThan", value:"true"}` = "distance > 10".
- **Ability/condition are Asset references by NAME** → resolve name → FNV hash → the existing
  `InvokeAbility(abilityHash, …)` / condition registry.

## Condition-type vocabulary (for later, richer enemies — NOT needed by the slice)

Across ~55 sampled Phases, the generic conditions are: **`Distance`**, **`Closest`**, `NearestPlayer`,
`RandomChance`, `NeedsFacingAndRange`, `EnemyNeedsDebuff`, `AllyNeedsBuff`, `LowAllyHealth`,
`MostIsolatedEnemy`, `FarthestWithMinimumDistance`, `BetweenOwnerAndTarget`. The rest are
encounter-specific (`ScaldronBoss_CanUseMelee`, `CitadelSpecialThree_Damage`, `VerdanthBoss_CanSpawn`,
pet `Condition_*`, etc.). Basic melee minions like `ZelemBasicMelee` use **none**.

## Aggro-population trigger (Ghidra confirm)

`AddAggroForObject` @`0x009fd690` is referenced only from DATA (`nGameObject` registrar @`0x00a091e2`)
— i.e. it is the Lua native `nGameObject.AddAggroForObject`. **Aggro is populated by Lua
perception/behavior scripts, not a hardcoded C++ perceive loop.** Our `AggroSystem` C# perceive-on-tick
is the slice's server-authoritative APPROXIMATION (documented divergence in the spec); porting the
retail Lua perception is a later refinement.

## Task-8/9 implications

1. AIController resolves `mpPhaseData` name → Phase asset (inject an `AssetDatabase`/resolver), reads
   `gambit[]`, and for `prioritizedList` picks the first gambit whose condition passes (absent
   condition = always true). For `ZelemBasicMelee` this is the single unconditional gambit.
2. `IAiActions.CastAbility` takes the ability NAME (resolve name → hash in `BridgeAiActions`, then the
   existing `InvokeAbility`). Movement for melee is the ability's own job — the AIController does not
   need a separate MoveToward for `ZelemBasicMelee`.
3. Condition evaluators (`Distance` etc.) are **out of the slice** — add when a conditional enemy is
   targeted.
4. In-game gate: spawn `ZelemBasicMelee` on the Dungeon path; hero enters perception → aggro → casts
   `ZelemBasicMeleeAttack` → (if that ability's Lua is loaded) pursues + hits → CombatEvent numbers.
   If `ZelemBasicMeleeAttack` is not yet a loaded Lua ability, the AI *decision* still fires (cast
   attempt logged) — that ability's script is a separate harvest.
