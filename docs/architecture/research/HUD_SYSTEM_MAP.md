# HUD System Map (Ghidra) — total-knowledge reference

Goal: know **exactly** what drives every in-game HUD element, so we stop guessing why the HUD is
blank. Ghidra client is the source of truth. Started 2026-07-13.

## The one big insight

Most of the HUD is **client-rendered from game state**, NOT from dedicated "HUD" wire messages. The
client already has the objects (via ObjectCreate 0x8C), their attributes (AttributeData 0x96 /
CombatantData 0x97), and the player state (LabsPlayerUpdate 0xA1). The Scaleform HUD + renderers read
that local state every frame. So a blank HUD element usually means **the underlying game state isn't
replicated correctly**, not that a HUD packet is missing.

The exceptions are a handful of **event** messages the HUD reacts to: CombatEvent (damage numbers),
Objective* (tracker), Cooldown (ability swirl), ServerEvent (FX).

---

## Per-element map

### 1. Floating damage/heal number + hit FX — ✅ wire confirmed
- **Wire:** CombatEvent **0xBA** (kGms 63). Handler `ClientNet::ProcessCombatEvent` @0x004e6190
  (body of `OnGmsCombatEvent` @0x0053ed50).
- **Struct** (reflection_serializer<8>): `0 flags(u16) · 1 deltaHealth(f32) · 2 absorbedAmount(f32) ·
  3 targetID · 4 sourceID · 5 abilityID · 6 damageDirection(Vec3) · 7 integerHpChange(i32)`.
- **Display gate (CRITICAL):** the number shows only when `localControlledObj = FUN_004e8e90() != 0`
  **AND** `deltaHealth > 0` (a POSITIVE MAGNITUDE, not a signed delta). Negative deltaHealth → falls to
  a branch that shows nothing.
- **ReCap status:** `CombatEventPacket` layout matches the struct (AssetReflection schema); `HealDamage`
  native broadcasts it with `|amount|` (commit 794d23d). **So damage numbers SHOULD render.** If they
  don't in-game, the likely cause is `localControlledObj == 0` (the deployed hero not recognised as the
  local controlled object) — verify that path, not the packet.

### 2. Player HP bar — ✅ same wire, driven by CombatEvent
- ProcessCombatEvent, when `targetID == localControlledObj`, computes `deltaHealth / GetMaxHitPoints()`
  and updates the hero HP bar color/threshold (FUN_004254e0/00425500 with low-HP color hashes
  0xfcd39570/0xdaa8ba9e/0x4a631908). So the HP bar is a side effect of CombatEvent on the local hero.
- Initial/authoritative HP comes from **CombatantData 0x97** + **AttributeData 0x96** at spawn.
- **ReCap status:** we send CombatantData + AttributeData at spawn and CombatEvent on damage → bar should
  track. Confirm the hero is the local controlled object (see #1 gate).

### 3. Objective tracker (mission HUD) — ✅ wire confirmed, Lua-driven now
- **Wire:** `ObjectivesInitForLevel` **0xB7** (kGms 56, `OnGmsObjectivesInitForLevel` @0x0053c820),
  `ObjectiveAdd` (kGms 75 @0x0053c970), `ObjectiveUpdated` **0xB8** (kGms 58 @0x0053bb10, reads 23 bytes:
  `u32 id · u8 slot(0xFF=all) · u8 value · u16·u16 · u8 flag · u32×3 data`), `ObjectivesComplete`
  (@0x0053bc50).
- **ReCap status:** objectives now run their Lua Init + Death event, and `SetObjectiveIntData` broadcasts
  ObjectiveUpdated (additive to the C++ fixture). ⚠ The current `ObjectiveUpdatedPacket` field ROLES
  diverge from the Ghidra 23-byte layout (same size, different meaning) — it "works" by byte-position
  accident. TODO: rewrite to the real layout `{id, slot, value(u8), …, flag, u32×3}` and drive slot=index.
- **⚠ 0xB7 stride:** the client parser reads **56 bytes/objective** (`u32 id + 4×u8 flags + 48B blob`);
  our packet sends **7 bytes/objective** (id + u24). It only "works" because ReadStreamBytes is
  short-read-tolerant → only objective #1 gets a partial id, #2..n zeroed. TODO: emit 56B/objective.

### 4. Ability cooldown swirl — ✅ wire confirmed
- **Wire:** CooldownUpdate **0xC1** (36B, `ApplyCooldownUpdate`). Key = {objId, abilityId}; relative mode
  (start=0 → client stamps end = now + duration).
- **ReCap status:** `PayCooldownAndMana` + the cooldown-cluster natives broadcast it. Confirmed decode.

### 5. Minimap (floor + enemy/object markers) — 🟡 client-rendered, no dedicated wire
- **System:** `SP_Graphics/MiniMapRenderer` — `buildMinimap`, `renderMinimapFloor`, `renderMinimapMarkers`,
  `setMinimapData`, `DrawMinimap`. The renderer iterates the client's **known game objects** and draws a
  marker per object (by team/type). There is NO per-frame minimap wire message.
- **Implication:** enemies appear on the minimap iff they are ObjectCreate'd AND flagged so the renderer
  picks them (team/combatant/interactable). TODO: confirm which object fields `renderMinimapMarkers`
  filters on (team? npcType? a "showOnMinimap" flag?) and that our enemy ObjectCreate/CombatantData set
  them. This is a game-state-replication check, not a new packet.

### 6. Mana bar / overdrive meter — 🟡 TODO
- Player mana + overdrive are in LabsPlayerUpdate 0xA1 / attributes. Overdrive effect is unimplemented
  (acks only). TODO: map the mana/overdrive HUD binding + which attribute id feeds it.

### 7. Crystals / DNA / deck HUD — 🟡 TODO
- Crystal count, DNA balance, deck slots. Driven by LabsPlayerUpdate + REST inventory. TODO: map.

---

## Method to complete this map
For each element: find its Scaleform binding or renderer (string search), decompile the handler, record
(a) the wire message or the game-state field it reads, (b) the exact struct/field, (c) the display gate,
(d) the ReCap status. Prefer confirming the **game-state field** over inventing a packet — the HUD mostly
reads local state.

Related: [[server-event-contract]] (0x9B FX), [[action-command-contract]], COMMAND_MATRIX.md (kGms table),
VERIFIED_FACTS.md.
