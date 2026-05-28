# Architecture — Consolidated Open Questions

Single register of every unresolved architecture question across ReCap, gathered from the asset-system reverse-engineering, the C++ reference server, and the original-source-tree leak (foehammer). Each row notes **how it could be answered** and **leverage** (impact if resolved).

> Sources: [`assetdata-system/GHIDRA_GROUND_TRUTH.md`](../assetdata-system/GHIDRA_GROUND_TRUTH.md), [`assetdata-system/FORMAT_COVERAGE.md`](../assetdata-system/FORMAT_COVERAGE.md), [`assetdata-system/ASSET_SYSTEM.md`](../assetdata-system/ASSET_SYSTEM.md), [`ORIGINAL_SOURCE_TREE.md`](../research/ORIGINAL_SOURCE_TREE.md), [`DEV_TESTIMONY_FOEHAMMER.md`](../research/DEV_TESTIMONY_FOEHAMMER.md), `PORTING_MATRIX.md`, and the AssetData.Parser redesign docs.

> **Update 2026-05-26:** the foehammer testimony ([`DEV_TESTIMONY_FOEHAMMER.md`](../research/DEV_TESTIMONY_FOEHAMMER.md)) answered several questions — see the ✅ rows. **foehammer is now unreachable** ("the dalkon effect"), so the *"ask foehammer"* channel is closed; remaining questions must go through Ghidra / C++ ref / experiment / decision.
> Answer channels: **Ghidra** (decompile client), **C++ ref** (dalkon's server), **foehammer** (original dev), **experiment** (run client/server + capture), **decision** (a design choice we make).

---

## 🔴 High leverage — resolve these first

| ID | Question | Current state | How to answer | Why it matters |
|---|---|---|---|---|
| Q1 | **Is `sdl` the asset-type schema-definition language?** (pinned folder in foehammer's tree) | Unknown. foehammer channel **now closed** (unreachable). foehammer *did* confirm `.noun` = the "property system" (distinct from model fixups). | **Ghidra** (the property/reflection system defines schemas at static-init — `Register` callers) | Would replace our 143 hand-transcribed C# stubs with ground truth. |
| Q2 | **`Simulation` (`n*`) architecture** — how `nGameSimulator` / `nObjectManager` / `nGameDirector` / `nBehaviorTree` / `nAbility` fit together | Class names known (Ghidra namespaces); internals not mapped. | **Ghidra** (decompile `n*`) + **foehammer** | The combat/AI/ability engine = ~75% of ReCap's `Game/` gap (`PORTING_MATRIX.md`). |
| Q3 | **Lua role** — does `nGameSimulator` drive `LuaManager`/`LuaScript` for abilities/objectives? Port the VM or stub? | Lua namespaces exist; integration unmapped. | **Ghidra** + **C++ ref** (`LuaFunctions.cpp`) + **decision** | Every `UseCharacterAbility` no-ops without it (Phase 10). Gates all combat scripting. |
| Q4 | **Field layouts of the 45 unported formats** (`Noun`, `PlayerClass`, `ability`, `labs*`, `ClassAttributes`…) | Names+addresses known (`FORMAT_COVERAGE.md`); field tables not decoded. | **Ghidra** (decompile `AssetData::Foo`) or **Q1** if `sdl` lands | Unblocks real creature/combat asset loading. |

---

## 🟡 Asset / reflection system

| ID | Question | Current state | How to answer |
|---|---|---|---|
| Q5 | `cAssetProperty` — does `value` (char[80]@0x58) ever overflow to a blob? Is `name@0x04` aligned? What are the ~20 trailing bytes of the 188B record? | Layout decoded; edges unverified. | **Ghidra** + inspect a real `.noun` with properties |
| Q6 | Catalog index maps **#1 / #3 / #5** exact keys | Confirmed auxiliary (off data path); keys unpinned. | **Ghidra** (decompile RegisterAssets / SetPriorityAsset) — low priority |
| Q7 | Port `BuildTypeMetadata` fingerprint to C#? | Algorithm decoded; not ported. | **decision** (cheap disk-cache invalidation key) |
| Q8 | Does a `noun` embed cross-refs as **asset IDs** or **name strings**? | Unknown (affects resolver design). | **Ghidra** + inspect parsed `.noun` |
| Q9 | `catalog_*.bin` name seeding vs external `reg_*.txt` in the parser | Both work; fidelity choice. | **decision** |

---

## 🟢 Network / protocol

| ID | Question | Current state | How to answer |
|---|---|---|---|
| Q10 | **`nSporeNet` ↔ Blaze boundary** — how SporeNet sat on EA Blaze | Both namespaces confirmed in binary; layering unclear. | **Ghidra** + **C++ ref** (`SporeNet/`, `Blaze/`) + **foehammer** |
| Q11 | Why is the `ChainVoteMsgs` 0x151 buffer **LE** while sibling buffers are BE? | Empirically frozen (works); root cause unknown. | **Ghidra** (decompile `ChainData::WriteTo`) |
| Q12 | Why must `LabsPlayerData.SetInitialDataBits` be exactly the **12 frozen bits** (not C++'s 16)? | Empirically frozen; theory unknown. | **Ghidra** (decompile `labsPlayer` ctor + `SetInitialDataBits`) |

---

## 🔵 Original modules (foehammer tree)

| ID | Question | Current state | How to answer |
|---|---|---|---|
| Q13 | `Docs/` — any internal design docs? | **dead** — foehammer unreachable; contents never shared. | — (channel closed) |
| Q14 | `ConsoleClient` / local server | ✅ **answered** — `ConsoleClient` = a **telnet client** to the client's built-in `ConsoleServer` (generic parser-registry command console over `TelnetTransport`, present in retail `Darkspore.exe`). Full map in [`CONSOLE_SYSTEM.md`](../research/CONSOLE_SYSTEM.md). foehammer's "probably compiled out" likely refers to a headless **server-only** build, not the console server itself. | done (port/listen-by-default still TBD — Ghidra) |
| Q15 | `Spark` / `Gears` / `Audio` purposes | ✅ **`Spark` answered** — embedded HTTP server `SP_App/HTTPServer` ("Spark HTTP Server / 1.01.00", port 8088); confirmed in `Darkspore.exe` (`FUN_007eb780`), evidence from Auntie Owl (Discord). `Audio` confirmed (build "all building except audio"). `Gears` still inferred. | `Gears` → **Ghidra** |
| Q16 | `UI_Flash` vs `UI_UTFWin` vs `UI_WebKit` | ✅ **answered** — EA-internal (ripped) → UTFWin (Spore; hero-editor UI) → Scaleform (most UI, top-level) → WebKit (TOSS/web). `DEV_TESTIMONY_FOEHAMMER.md` §3. | done |
| Q17 | Original tree shareable? | **dead** — foehammer offered, then went unreachable; only descriptions obtained. | — (channel closed) |

---

## ⚪ AssetData.Parser redesign (engineering, our decisions)

| ID | Question | Current state | How to answer |
|---|---|---|---|
| Q18 | `AssetNode` leanification — editor-node adapter shape after moving MVVM out of L1 | Identified (redesign §9); not designed. | **decision** + spike |
| Q19 | Keep one `DataType` enum, or split sentinels vs value-types? | Open (redesign §8). | **decision** |
| Q20 | Order of work: .NET 10 sweep first, or registry refactor first? | Leaning .NET 10 first (independent, low risk). | **decision** |
| Q21 | `sdl` import (Q1) — if obtained, regenerate stubs from it vs keep hand-authored DSL? | Depends on Q1. | **decision** (after Q1) |

---

## Answer-channel summary

> The **foehammer channel is closed** (unreachable since 2025-08). Everything now routes through Ghidra / C++ ref / experiment / decision.

- **Decompile in Ghidra (highest leverage now):** Q2 (`Simulation` `n*` — the combat engine), Q4 (format layouts), Q1 (reflection/property schema source), Q5/Q8 (cAssetProperty/cross-refs), Q11/Q12 (frozen LE/bits root cause), Q3 (Lua wiring).
- **Our decision:** Q7, Q9, Q18–Q21.
- **Closed / dead:** Q13, Q17 (needed foehammer).

> With foehammer gone, the highest-leverage move is **decompiling the `Simulation` `n*` namespace (Q2)** — it's the combat/AI engine and the dominant gameplay gap, and we have the full client binary.
