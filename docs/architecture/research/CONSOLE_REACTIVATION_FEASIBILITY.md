# Console Reactivation Feasibility — retail `Darkspore.exe`

**Question:** can the game's external local command console (the one foehammer used "via host or web") be reactivated in the *retail/ship* client?

**Date:** 2026-07-13. **Method:** 4-angle parallel Ghidra RE (`Darkspore.exe`, image base `0x00400000`), read-only. Supersedes the open questions in [`CONSOLE_SYSTEM.md`](CONSOLE_SYSTEM.md).

---

## TL;DR verdict

| Route | Feasible? | Confidence |
|---|---|---|
| **No-injection** (CLI switch / AppProperty / config gate) | **NO** — start-path is genuine dead code, not gated | HIGH |
| **Injection — main-thread call is SAFE** | **YES, PROVEN LIVE** (no crash) | VERIFIED |
| **Injection → HTTP `:8088`** | **NO** — object builds but never binds (pump absent) | VERIFIED |
| **Injection → telnet** | **LIKELY** — `TCPInterface::Listen` self-binds, no pump needed | next experiment |

> **★ LIVE-VERIFIED 2026-07-13 (x32dbg on retail 5.3.0.127).** The injection *mechanism* works — calling the dormant init on the main thread does **not** crash. But the HTTP service is a deeper dead end than expected: the object builds and "starts" (flags only) yet **never opens a socket**, because its servicing pump does not exist in retail. See "Live experiment" below. Pivot the reactivation target to the **telnet** path, whose `TCPInterface::Listen` binds synchronously with its own accept thread (no pump).

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

## Live experiment — 2026-07-13 (x32dbg, retail 5.3.0.127, PID attach, base 0x00400000, no ASLR)

Ran Stages 0–3 of the test plan against the live in-game client (main menu; `mb132_x32`+`eawebkit`+`d3d9`+`physxloader` loaded).

**Stage 0 — baseline.** `Get-NetTCPConnection` for `Darkspore.exe`: only Blaze `127.0.0.1:42125` + 2 UDP. Nothing on `:8088`. Dormant confirmed.

**Stage 1 — SP_App located (read-only).** `findallmem 0,"58 C5 00 01"` → 3 hits: two in `.text` (ctor `0x007EC869`, dtor `0x007ECADB`), one heap = **`SP_App` = `0x04042CE8`** (this run; heap, per-run). Reads: `+0x00` = `0x0100c558` (vtable ✔ right object), `+0x16c` = `0x00000000` (HTTP ptr NULL = dormant ✔). `+0x140` = `0xD3657200` (NOT 0/1 → the Angle-C "devDirs at +0x140" offset guess is **wrong**; irrelevant to this path).
> Note: at the **launcher** stage the heap hit is absent (only the 2 code hits) — `SP_App` isn't constructed until you hit Play. Confirms EAWebKit loads before the game's `App::Init`, i.e. the timing-independent `PeekMessageW` hook is the right foothold.

**Stage 2 — main-thread call, NO CRASH (the make-or-break, PASSED).** Caught the main thread (`Mb132UiThread`, TID 25516) at a conditional bp on `user32.PeekMessageW` filtered to a darkspore.exe caller (`[esp]` in `0x400000..0x17f0000`; return addr `0x00B366C3` = the game's own pump — a clean point). Hijacked context: pushed a catch address (allocated page + sw bp), set `ECX=0x04042CE8`, `EIP=0x007EB780`, ran. `FUN_007eb780` **returned cleanly to the catch, no crash**. Restored full context afterward; game resumed normally. **This validates the entire injection hypothesis — the 2026-05-27 remote-thread crash was purely a wrong-thread/stale-`this` artifact, not intrinsic.**

**Result of the call:** `+0x16c` went `NULL → 0x2A317E00`; `eax` = `0x2A317E00`. Object vtable at `0x2A317E00` = `0x01051db0` = "EA HTTP Server 1.0" — a valid HTTPServer was constructed and stored.

**Stage 3 — FAILED: no socket.** After resume, still nothing on `:8088`. Root cause traced in Ghidra:
- "Start" `FUN_00b2fde0` (vtable `+0x10`) does **not** bind — it only `Lock`s, flips `+4` (started) and calls vtable `+0xd8` = `FUN_00b30130`, which merely sets flag `+0x1ec`. Pure state toggle.
- `FUN_007eb780` then calls `FUN_007b3830(server)`, which stores the server ptr into global `_DAT_01464f50`.
- **`get_xrefs_to(0x01464f50)` → ONE xref, the WRITE itself. Zero readers.** Nothing in retail ever reads the HTTP singleton → no pump services the "enabled" flag → the socket is never created. The EA HTTP Server is gutted even more thoroughly than the console: not only is its start unwired, its entire servicing loop is absent.

**Conclusion:** injection is safe and real, but **HTTP is a dead end** (object without a pump). The **telnet** path is the viable target: `TelnetTransport::Open` → `TCPInterface::Listen` (`FUN_00ad3c40`) does `socket/bind/listen` + spawns its own accept thread **synchronously on the calling thread** — no external pump required. Next experiment: construct a `ConsoleServer`+`TelnetTransport` (or find a dormant instance) on the main thread and drive `SetTransport(transport, port)`; resolve the `SetTransport` param types via `FUN_00accab0`/`FUN_00aae930` first.

**Debugger notes (for next session):** mb132 (Chromium/miniblink) thread-churn + x32dbg auto-`singleshoot` TLS-callback bps make it pause constantly during load — **detach during launcher, re-attach at the idle in-game menu, then `bc` all the TLS bps**. Attach lands on a random worker thread; use a caller-filtered `PeekMessageW` bp to catch the real main thread at a clean point. Catch-return via allocated page + sw bp works cleanly for hijack-call-restore.

## Telnet enable recipe — 2026-07-13 (Ghidra prep, step 1 done)

Full decompile of the telnet chain. Unlike HTTP, this path **self-binds** and needs **no dormant pump of its own** for the socket — only our periodic `ProcessCommand` pump for dispatch.

**The "fake ConsoleServer" insight:** both `SetTransport` (`FUN_00aad990`) and `ProcessCommand` (`FUN_00aadbe0`) touch their `this` only as `*param_1` = the transport pointer. So the ConsoleServer can be a **single 4-byte slot** `S` holding the transport pointer — no real ConsoleServer object needs constructing. Pass `&S` as `this` to both.

**Objects:**
- **TelnetTransport** — ctor `FUN_00ab6250(mem)` sets vtable `0x0103c8dc`, zeroes fields. Over-allocate ~`0x40` zeroed bytes (exact factory size unconfirmed; ctor + bases touch offsets 0/4/0x14/0x18). Slot `+4` = its internal `TCPInterface` (Open lazy-allocs `0x250` there via `FUN_00ab6ce0`). Only ever built by the reflective `CreateObject` factory in retail — we build it directly.
- **Slot `S`** — 4 zeroed bytes; becomes the transport pointer after `SetTransport`.

**Enable (binds the socket, synchronous):**
`SetTransport(&S, T, port)` = `FUN_00aad990(this=&S, transport=T, port)`:
1. `*(&S) = T`
2. `T->Open(port,1)` (transport vtable `+4` = `FUN_00ab6310`) → `TCPInterface::Listen` (`FUN_00ad3c40`): `socket(AF_INET,SOCK_STREAM)` → `bind(htons(port), INADDR_ANY)` → `listen(64)` → spawns accept thread `FUN_00ad5000` via `_beginthreadex`. **Socket is live on return.**
3. registers the transport with the global parser registry.

**Dispatch (needs our pump):** the accept thread (`FUN_00ad5000`) only accepts + **queues** received lines — it does not dispatch. `ProcessCommand(&S)` (`FUN_00aadbe0`) is the consumer: drains the queue, sends the greeting `"Connected to remote command console.\r\nType 'help' for help.\r\n"` on new connect, and dispatches `help`/`quit` (hardcoded builtins — always work) + parser commands (global registry). **It must be called repeatedly** (e.g. once per frame from the `PeekMessageW` hook). No retail code calls it → we pump it.

**Live-proof procedure (x32dbg, main thread — same hijack technique as the HTTP test):**
1. `alloc T` (0x40, zero); call `FUN_00ab6250(T)`.
2. `alloc S` (4 bytes, zero).
3. On the main thread (caught at the `PeekMessageW` bp): `ecx=&S`, push `T`, push `port` (pick our own, e.g. `0x23F0`=9200), `call 0x00aad990`.
4. `netstat` → port LISTENING under `Darkspore.exe` ⇒ **socket proven**.
5. `ncat 127.0.0.1 9200` (raw, no telnet IAC).
6. Pump: main thread `ecx=&S; call 0x00aadbe0` a few times ⇒ greeting appears, `help` responds ⇒ **console proven 100%**.

**Runtime unknowns (resolve live; none are blockers):** (a) the global parser-registry `this` that `FUN_00accab0`/`FUN_00aae930` load into `ecx` — verify valid when `ProcessCommand` runs; (b) whether command parsers (editor/prop) are actually populated (affects `help` richness only — greeting + `help`/`quit` builtins work regardless); (c) exact TelnetTransport size (over-allocate).

**Fase B (native):** one-shot `SetTransport` at enable time + `ProcessCommand(&S)` each frame, both from the existing main-thread `PeekMessageW` hook in `recaphooks.cpp`.

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
