# Console Reactivation Feasibility — retail `Darkspore.exe`

**Question:** can the game's external local command console (the one foehammer used "via host or web") be reactivated in the *retail/ship* client?

**Date:** 2026-07-13. **Method:** 4-angle parallel Ghidra RE (`Darkspore.exe`, image base `0x00400000`), read-only. Supersedes the open questions in [`CONSOLE_SYSTEM.md`](CONSOLE_SYSTEM.md).

---

## TL;DR verdict

| Route | Feasible? | Confidence |
|---|---|---|
| **No-injection** (CLI switch / AppProperty / config gate) | **NO** — start-path is genuine dead code, not gated | HIGH |
| **Injection** (Detours one-shot on main thread) | **YES** — HTTP on `:8088`, low risk | HIGH |

The console classes (`ConsoleServer`, `TelnetTransport`, Spark `HTTPServer`) **ship compiled into retail** but the code that would *start* them was compiled out of `App::Init`. No flag restores it. The only way in is to add the missing start call ourselves via injection — which the existing EAWebKit/Detours foothold (`recaphooks.cpp`) already makes cheap. See [[console-system-telnet-server]], [[eawebkit-redirect-and-ports]].

---

## Angle A — the negative proof (start-path is dead)

Both start entry points are unreachable at **three independent levels**, not merely gated behind a missing flag:

**Telnet — `ConsoleServer::SetTransport` `FUN_00aad990`:**
- `get_xrefs_to` → **0 references**.
- Byte-search for the LE pointer `90 D9 AA 00` across the whole image → **no match**. The function's address exists **nowhere** in data → no vtable/table/callback can reach it indirectly either.
- The vtable `TelnetTransport::Open` lives on (`0x0103c8e0`) has **0 xrefs** → no `TelnetTransport` object is ever constructed.

**HTTP — SP_App setup `FUN_007eb780` (vtable slot +0x90 @ `0x0100c5e8`):**
- Byte-search for `80 B7 7E 00` → **exactly one hit**: `0x0100c5e8`, the vtable slot itself. Nowhere else.
- The only 7 `CALL [reg+0x90]` sites in the entire binary (in `FUN_00d92160`/`FUN_00d92730`) operate on **unrelated objects** (string/URL-decode callbacks, vtables in the `0x0109ac..0x0109d5` range) — vtable-offset coincidence, never resolve to `0x0100c5e8`.
- `App::Init` (`FUN_00538090`) and SP_App-init (`FUN_007eb970`) call SP_App slots `+0x94`/`+0x98` but **never `+0x90`**, unconditionally, no gated branch.

**Conclusion:** the start calls are absent from the binary entirely. Not stripped-and-gated — the call instruction does not exist.

## Angle B — no switch/property gate exists

- Enumerated **all** callers of the CLI switch checker `Core::GetCommandLineOption` (`FUN_00aed790`) → **30 recognized switches**, none console/telnet/HTTP/server/port related. (Full inventory below.)
- `devDirs`/`shipDirs` flag (`SP_App+0x140`, set in `FUN_007ed2a0`) is **purely a filesystem-mount toggle** — traced every read, no console/network branch.
- `ConsoleServer`/`TelnetTransport` `.cpp` source-path strings survive in ship (proves not stripped) — the command dispatch loop `FUN_00aadbe0` is fully functional. What's missing is the *caller*, not the class.
- `TelnetTransport` ctor is reachable only via `FUN_00a97840` (RakNetworkFactory), which itself has **0 callers** → dead registration.

**Verdict:** wiring compiled out, not feature-gated. No undiscovered flag can help.

**Open (low-probability) follow-ups, not yet done:**
1. FNV-hash brute-force of AppProperty names against a console wordlist (`emulate_hash_batch`) — the one way a hidden property gate could exist that string-search misses.
2. `localCheats.txt` ArgScript command table — confirm no data-driven command reaches the transport factory.

### Command-line switch inventory (retail, 30 switches)

`devDirs`/`shipDirs` (mount), `dataDir`, `userDataDir`, `writeableData`, `elevate`, `testPatch`, `patch`, `upgrading`, `compileData`, `multipleInstances`, `nolauncher`, `headless`, `demo` (discarded), `noAsserts` (disables AppConsole/AppDebugger assert parsers — *opposite* of a console enable), `automation` (inverted), `noReplay`, `noLocalReplay`, `flock`, `safe`, `vSync`/`noVSync`, `noPlugins`, `noScripts`/`noMaterialScripts`/`noEffectScripts`, `noDevEffects`, `showConfigAlerts`, `noSound`, `nofocus`, `locale`, `LogNetworkStats`, `dumpShaders`/`dumpShadersForPIX`/`dumpFragmentShaders`/`dumpDirectShaders`.

## Angle C — injection IS viable (the way in)

The remote-thread experiment (2026-05-27) crashed because it called `FUN_007eb780` off an **AOB-scanned, possibly-stale `this`**, on a thread with **no engine SEH/TLS bookkeeping**, potentially **racing SP_App's constructor**. None of those are intrinsic to the function — its body is a plain null-checked `construct → configure → start` on `SP_App+0x16c` only.

**`FUN_007eb780` decompiled behavior:**
```c
if (this->field_0x16c == NULL) {           // HTTP server slot, NULL when dormant
    obj = MemAlloc(0x298, "SP_App/HTTPServer");   // ctor FUN_00b35130, vtable 0x01051db0
    // refcounted assign into this+0x16c
    (**(vtbl+0x2c))("ServerVersionName ... Port 8088 ...");   // config — PORT 8088 literal
    if ((**(vtbl+0x10))()) FUN_007b3830(this+0x16c);  // HTTPServer::Start = FUN_00b2fde0
    else release;
}
```

**Safe main-thread hook point:** `App::Init` = `FUN_00538090` runs once, synchronously, on the main thread, returns `1` on success after full SP_App construction. Hook it, call original, and **on non-zero return** call `FUN_007eb780(this)` using the *same* `this` (`ECX`/`param_1`) passed into `App::Init` — not an AOB re-derived pointer. Guarantees: fully-constructed `SP_App*`, correct thread, engine SEH/TLS already established, no new-thread races.

**Telnet path (Option 2):** the live console-registry singleton already exists post-init — accessor `FUN_00b1d370` (hash `0x23ab34a1`), called from `App::Init` and the CheatManager registrar `FUN_00866680`. From the same hook point, get the singleton and wire a transport via `FUN_00aad990`. **One unknown remains:** the exact `param_1` type of `SetTransport` and the transport vtable layout — decompile `FUN_00accab0`/`FUN_00aae930` (listener iteration) before wiring live. `TelnetTransport::Open`→`TCPInterface::Listen` (`FUN_00ad3c40`, socket/bind/listen + `_beginthreadex` accept loop) is itself thread-agnostic; only the ConsoleServer wiring is uncertain.

## Angle D — usability once enabled

**Ports:** HTTP = **8088** (literal in `FUN_007eb780`'s config string). Telnet = **statically unrecoverable** (port is a `u_short` arg supplied through the never-called vtable chain; no config-key string exists). Recover the telnet port dynamically: breakpoint `FUN_00ab6310` (`TelnetTransport::Open`) entry or `bind`, read the arg — but this is moot if we inject and choose the port ourselves.

**What the console actually controls** — it is a **rendering / effects / app-control / model-editor dev console**, NOT a gameplay cheat console. There is **no `spawn`/`teleport`/`godmode`/`give`** (those strings belong to the unrelated ArgScript/Lua tables). Full registrant sweep via `FUN_00af2440` (10 groups) + native `AddParser` parsers:

| Group / parser | Automation value | Notable commands |
|---|---|---|
| **State manager control** (`0x00861570`) | ★★★ drives the game-state machine | `[<state>]`, `-list`, `-next`, `-prev`, `-reload` |
| **Application control** (`0x007ef050`) | ★★ | `-pause`, `-unpause`, `-speed <f>`, `-step <n>`, `-quit`, `-url <s>`, `-alloc <n>`, `-lock`/`-unlock`, `-listPacks`, `-listAddOns` |
| **Effect manager** (`0x00cc8d80`) | ★★ world state | `-createWorld <id>`, `-removeWorld <id>`, `-state <worldState>`, `-worlds`, `-list`, `-kill`, `-memStats`, `-sunDir`, `-windDir` |
| **Effect spawner** (`0x00cc9010`) | ★ visual | `[<effectName>]`, `-pos x y z`, `-model`, `-texture`, `-scale`, `-start`/`-stop` |
| **editor** (native) | model/creature editor | `load`, `save`, `rename`, `zcorp`, `validate`, `dumpPalettes`, `dumpGeomInfo`, `skinPaintValues` |
| console-password (native) | auth mgmt | `SetPassword`, `ClearPassword`, `GetPassword` |
| Movie / Light / Material / RenderTarget / Baker / PaintEffect cheats | dev/debug | recording, dumps, bake control |

**Telnet protocol:** raw TCP, **no IAC negotiation** (do not send `IAC WILL/DO` — `0xFF` isn't special-cased). Server-side local echo per char; single-slot history via `ESC [ A` (up-arrow, `1B 5B 41`); send `command\r\n`. Greeting `"Connected to remote command console.\r\nType 'help' for help.\r\n"`. Builtins `help`/`quit`, dispatch by parser name or 1-based index (tokenizer splits on space, honors `"`). A bare TCP client works.

**Auth:** default password state is **unset** (`"No password is set."`). No pre-command auth gate was found in the dispatch path — an open console once the port is reachable. (Confirm dynamically; absence of a guard branch isn't 100% proof.)

---

## EAWebKit load-timing caveat + timing-independent hook

The clean "hook `App::Init` return" point (Angle C) assumes our Detours foothold is installed **before** `App::Init` runs. It may not be: the client loads `EAWebKit.dll` from inside the **WebKit module init** (`FUN_00ca6720`, tag `"SP_App/WebKit"`, reached via module vtable `0x01079dc0`) — a higher-level subsystem started **after** `SP_App` is constructed. So our `DllMain`→`RecapHooksInstall` may run after `App::Init` already returned, and we'd miss the one-shot.

**This does NOT block reactivation.** Option 1 only truly requires (a) a fully-constructed `SP_App*` and (b) main-thread execution. If EAWebKit loads late, `App::Init` has already finished → `SP_App` exists → AOB-scan for it is *safe* (the racing-ctor crash cause is gone). The main-thread requirement is met by piggybacking the one-shot on a hook we **already** install and that the game calls every frame on the main thread: **`Hook_PeekMessageW`** (`ReCapHooks.cpp`). This is fully timing-independent — by the time any frame pumps messages, `App::Init` is long done.

| Approach | Needs EAWebKit loaded early? | Gets `this` free? | Timing-independent? |
|---|---|---|---|
| Hook `App::Init` return | Yes | Yes | No |
| **One-shot in `PeekMessageW` hook** | **No** | No — AOB-scan (safe post-init) | **Yes** ← use this |

**Option 1 sketch (on the existing `PeekMessageW` hook):**
1. Gate behind a `recap.cfg` flag (default OFF) — opt-in only.
2. Static one-shot guard. On each `Hook_PeekMessageW`, if not-yet-done and flag on:
3. AOB-scan process memory for a 4-byte location holding the SP_App vtable ptr `58 C5 00 01` (`0x0100c558`). Expect exactly one hit = the live `SP_App*` (heap, per-run). Sanity-check `+0x140`==0 (ship) and `+0x16c`==NULL (HTTP dormant).
4. Call `FUN_007eb780(sp_app)` as `__thiscall` (this in ECX — use a typed `__thiscall` fn-ptr or a small `mov ecx,this; call` stub). Idempotent: the function only builds if `+0x16c`==NULL.
5. Set the guard; log result. HTTP server comes up on `:8088`.

## Recommended path if we proceed

1. **Prove the injection mechanism** with the lowest-risk target: Detours one-shot hook at `App::Init` return (`0x00538090`), main thread, call `FUN_007eb780(this)` → **Spark HTTP server on `:8088`**. Zero remaining unknowns.
2. Confirm it listens (`netstat`) and responds (browser/HTTP to `:8088`).
3. Only then attempt the telnet path (Option 2) — resolve the one open unknown (`SetTransport` param type via `FUN_00accab0`/`FUN_00aae930`) first.
4. Reality check on value: this console is **effects/app/editor control**, not gameplay cheats. Best real levers for ReCap porting work = **State manager** (`-next`/`-prev`/`<state>`) and **Application control** (`-pause`/`-speed`/`-step`). Weigh against just building a ReCap-side console we fully control (see [`CONSOLE_SYSTEM.md`](CONSOLE_SYSTEM.md) "Recreating the console in ReCap").

## Test plan — prove it before writing a hook

Validate the whole theory **live in a debugger first** (x64dbg/Ghidra debugger MCP, no build/deploy), then codify. Each stage gates the next.

**Stage 0 — baseline (negative).** Launch retail via ReCap. `Get-NetTCPConnection` filtered to `Darkspore.exe` → confirm **nothing** on `:8088`. Establishes the dormant state.

**Stage 1 — read-only validation (zero risk).** Attach x64dbg to the live client at a menu. AOB-scan for `58 C5 00 01` → confirm exactly one hit = `SP_App*`. Read `+0x140` (expect 0 = ship) and `+0x16c` (expect NULL = HTTP dormant). Proves we have the right object and it's off. No writes, no calls.

**Stage 2 — manual call from debugger (main thread).** Set a breakpoint on the main thread (e.g. at the message pump / a per-frame func). When hit, set `ECX = sp_app` and `call 0x007eb780`. Watch: no crash, `+0x16c` becomes non-NULL. This is the make-or-break test — if it survives here on the main thread, the injected version will too.

**Stage 3 — confirm listen.** `netstat` again → `:8088` LISTENING under `Darkspore.exe`.

**Stage 4 — confirm response.** `curl http://127.0.0.1:8088/` (or browser) → EA HTTP Server responds.

**Stage 5 — codify.** Only now port the Stage-2 sequence into `Hook_PeekMessageW` (one-shot, `recap.cfg`-gated), rebuild `EAWebKit.dll`, and repeat Stages 3–4 with zero manual debugger steps.

Cheapest first move = **Stages 0–2 in x64dbg** — proves or kills the plan in minutes without touching code.

## Key addresses

| Addr | Symbol / role |
|---|---|
| `0x00538090` | `App::Init` — **main-thread hook point** (call setup after non-zero return) |
| `0x007eb780` | SP_App HTTP-setup — **Option 1 target**, port 8088, deref `this+0x16c` only |
| `0x0100c558` / `+0x90 @ 0x0100c5e8` | SP_App HTTP vtable / dead setup slot |
| `0x00b2fde0` | `HTTPServer::Start` (returns 1) |
| `0x00aad990` | `ConsoleServer::SetTransport` — telnet start, 0 xrefs |
| `0x00ab6310` | `TelnetTransport::Open(port)` → `TCPInterface::Listen` |
| `0x00ad3c40` | `TCPInterface::Listen` (socket/bind/listen, backlog 64) |
| `0x00aadbe0` | `ConsoleServer::ProcessCommand` (dispatch loop, functional) |
| `0x00b1d370` | live console-registry singleton accessor (hash `0x23ab34a1`) |
| `0x00866680` | AppConsole/CheatManager registrar |
| `0x00af2440` | generic cheat-command-group builder (10 registrants) |
