# Darkspore Cheat / Dev-Console Command Reference

Recovered from Ghidra decompile of retail `Darkspore.exe` (image base `0x00400000`). These are the commands the dev console (telnet or in-game) and `localCheats.txt` accept. See [`CONSOLE_REACTIVATION_FEASIBILITY.md`](CONSOLE_REACTIVATION_FEASIBILITY.md) for how the console is reactivated and [[console-system-telnet-server]].

## How it works

- **Registry singleton** = `FUN_007b3760()` → `DAT_01464f54`. vtable: `+0x18` Register(name, obj, flags), `+0x1c` Unregister(name), `+0x28` HasCommand(name).
- **Dispatcher** = `FUN_00b54650`: tokenizes the line, looks up the **first token**, calls the command's Execute with the rest. Unknown token → throws `"Unknown command"`.
- **Grouped commands**: the long string in `FUN_00af2440(obj, "help text", "-flag^", id, ...)` is **help text, not the typed token**. The typed token is a short word (`app`, `state`, …); flags follow it: `app -pause`, `state -list`.
- **`localCheats.txt`** (in `<Darkspore>\Data\Config\`) is fed through the **same** parser/dispatcher — one command per line. ✅ **Confirmed working 2026-07-13**: a `quit` line made the game launch and immediately close.

## Command tokens

### App / Core

| Token | Args | Description |
|---|---|---|
| `quit` | — | Quit the app |
| `app` | `-listPacks` \| `-listAddOns` \| `-pause` \| `-unpause` \| `-quit` \| `-speed <float>` \| `-step <int>` \| `-lock` \| `-unlock` \| `-minimize` \| `-url <cstring>` \| `-top` \| `-resetAsserts` \| `-alloc <int>` | Application control: list packages/add-ons, pause/unpause, sim-speed multiplier, single-step ms, lock-to-GPU, minimize, open URL, bring-to-front, reset asserts, test-allocate N bytes |
| `state` | `[<state>]` \| `-list` \| `-next` \| `-prev` \| `-reload` | State-manager control; no args = show current state; switch/list/step states, force script reload |
| `chain` / `endChain` / `loadResource` | — | App animation-chain control; force-load a resource |
| `option` | (obj) | Config/option-file control |
| `killallhints` | — | Dismiss all active UI hint popups |

### Graphics / rendering

| Token | Description |
|---|---|
| `mr` | ModelRendererCheat — model-renderer debug toggle |
| `lr` | LightRendererCheat — light-renderer debug toggle |
| `vb` / `ib` | VertexBuffer / IndexBuffer debug cheat |
| `scriptRenderer` | Script-driven renderer toggle |
| `blocksmode` | Creature-animation "blocks mode" debug view |
| `camera` | Free/debug camera control |
| `paintEffect` | Effect/particle "paint" tool |
| `prop` / `listProps` | Inspect / list ArgScript properties |

*(Flag lists for `mr`/`lr`/`vb`/`ib`/`scriptRenderer`/`camera` not yet decompiled — only their registration ctors. Follow-up: decompile their Execute vtables `PTR_FUN_01015c78` (vb), `PTR_FUN_01015cf8` (ib), builders `FUN_004a3960`/`FUN_004a37d0` (mr/lr).)*

### Editor (SP_Editor — likely need editor mode active)

| Token | Description |
|---|---|
| `addDNA` | Add DNA/creature-part item |
| `freedom` | Unlock/free-placement mode |
| `toggleeditorbackground` | Toggle editor background render |
| `colladaexport` | Export model/scene to Collada (.dae) |

### Audio / Baker

| Token | Description |
|---|---|
| `baker` | Asset baker (offline processing) subsystem |
| `usermusic` | User-music / soundtrack control |
| `aev` / `asc` | Short audio-engine tokens (semantics unconfirmed) |

## Syntax examples (console line = localCheats.txt line)

```
app -pause
app -speed 0.25
app -step 16
app -top
state
state -list
state -next
state -reload
listProps
killallhints
```

## Safe / observable first tests

```
app -speed 0.25     # game runs visibly slow (reversible: app -speed 1.0)
app -top            # window to front — harmless
state -list         # introspection → log/console
listProps           # introspection → log/console
```

Avoid on first pass: `quit`, `app -quit` (close game — already confirmed working), `app -alloc <large>` (OOM stress), editor cheats (need SP_Editor mode).

## Open items

- Recover the exact `Register("app", …)` call site (name proven via teardown `Unregister("app")` in `FUN_007eb530`).
- Decompile Execute bodies of the renderer cheats (`mr`/`lr`/`vb`/`ib`/`scriptRenderer`/`camera`) for their flag lists.
- Resolve `aev`/`asc` (SP_Audio init).
- ~14 of ~32 `FUN_007b3760` callers not yet opened — likely a few more simple commands.

## Registrar source addresses

`FUN_007ef720`/`FUN_007ef050` (app, quit), `FUN_00861820`/`FUN_00861570` (state/chain), `FUN_0085df00` (option), `FUN_00750800` (killallhints), `FUN_004a3c70`/`FUN_004a3780` (mr/lr), `FUN_00895370`/`FUN_008952e0` (vb/ib), `FUN_008cf520` (scriptRenderer), `FUN_009513b0` (blocksmode), `FUN_00867c70` (camera), `FUN_00472310` (paintEffect), `FUN_007f2460` (prop/listProps), `FUN_007223a0`/`FUN_00725200` (editor cheats), `FUN_00e797a0` (baker), `FUN_00c5ffd0` (usermusic/aev/asc).
