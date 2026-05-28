# Console System — `Darkspore.exe` (client) reverse-engineering

What the foehammer source tree calls **`ConsoleClient`** is one end of a generic **remote command-console** built into the retail client. This maps the whole system from Ghidra (`Darkspore.exe`, image base `0x00400000`) so ReCap can decide whether to recreate the server side.

> **Headline:** the console is a generic **Telnet command server** (EA's standard `ConsoleServer` + parser-registry pattern) that is **compiled into the retail client**. The original `ConsoleClient` project was just a telnet client to it. "Using the console" = speaking line-based telnet to a parser registry — no bespoke protocol.

---

## Three distinct "console" subsystems (don't conflate)

| Subsystem | Evidence | Nature |
|---|---|---|
| **Remote command console** (this doc) | `ConsoleServer.cpp`, `TelnetTransport.cpp`, `"Connected to remote command console.\r\nType 'help' for help."` @`01039560`, `PacketConsoleLogger` | TCP **telnet** server, password-protected, parser registry |
| **In-game cheat/debug console** (UI) | `AppConsole` @`01007480`, `AppConsoleUTFWin` @`0101e55c`, `SP_UI/DebugConsole` @`0101e5a4`, `ConsoleWindow` / `UTFWin/EA::UTFWinControls::ConsoleWindow`, `Lucida Console` font, `"Clears the debug console"` | UTFWin overlay (the `~` cheat console) over the **same** parsers |
| **Noise (not the command console)** | Blaze `ConsoleLoginResponse` / `CLIENT_TYPE_CONSOLE_USER` (= Xbox/PS3 *game console*), Win32 `WriteConsoleW`/`GetConsoleMode` (stdio), RenderWare audio docs ("printed to the console"), `cTransportRakNet` `"connecting to remote system [port=%u]"` @`0103522c` (= SporeNet game net, NOT console) | unrelated |

---

## Architecture (remote command console)

```
CheatManager (SP_App/CheatManager)
  └─ owns AppConsole + registers parsers
        ConsoleServer (ConsoleServer.cpp)
          ├─ parser vector  ──────────────► [Parser 0] [Parser 1] ... (named command registries)
          ├─ PacketConsoleLogger (+0x210)   each parser = vtable of commands
          ├─ listener / transport (+0x410)
          └─ TelnetTransport (TelnetTransport.cpp)  ──► TCP telnet client (the "ConsoleClient")
```

### Key functions

| Address | Role | Notes |
|---|---|---|
| `0x00aadbe0` | **`ConsoleServer::ProcessCommand`** (dispatch loop) | virtual (no direct xref — vtable). Greeting, `help`/`help <parser>`/`help <cmd>`/`quit` builtins, routes command to parser by name or 1-based number prefix, tokenizes via `FUN_00ad20a0` (space `0x20`, quote `0x22`) |
| `0x00aada70` | **`ConsoleServer::AddParser`** | dedups by pointer **and** by name (`vtable+4`=GetName); assert `ConsoleServer.cpp:0x43` |
| `0x00accab0` | `ConsoleServer::GetParserCount` | loop bound |
| `0x00aae930` | `ConsoleServer::GetParser(i)` | |
| `0x00aae950` | `ConsoleServer::AddParser` (internal push) | |
| `0x00aad950` | listener teardown | releases `+0x410`; assert `ConsoleServer.cpp:0x18` |
| `0x00ab2bb0` | `SetPacketConsoleLogger` | sets `+0x210`; if set, names `"PacketConsoleLogger"` — routes log output to the connection |
| `0x00ab5920` | password-command help text | `"Provides a secure connection… Used to modify the console password."` |
| `0x00ab62a0` | **`TelnetTransport::~TelnetTransport`** | vtable `0x0103c8dc`, frees 2 sockets (`TelnetTransport.cpp:0x1d/0x1f`) |
| `0x00ab6250` | **`TelnetTransport` ctor** | vtable `0x0103c8dc`; instantiated via reflective object factory (`CreateObject @ 0x00a97bc0`), not `new` |
| `0x00866680` | **`AppConsole` registrar** | allocs `SP_App/CheatManager`, builds `AppConsole` (`FUN_00865980("AppConsole", …)`), binds it (`+0x30("AppConsole",0,100)`, `+0x30("AppConsole","Console",1)`); itself a vtable entry of `AppConsole` (`PTR_FUN_01014594` @ data `0x01014884`) |
| `0x00aad990` | **`ConsoleServer::SetTransport(transport, port)`** | stores transport, calls `transport->Open(port)` (vtable `+4`), then registers every parser with it. **The `port` (a `u_short`) enters here** — no direct/data xref (invoked via vtable from boot wiring) |
| `0x00ab6310` | **`TelnetTransport::Open(port)`** (vtable `+4`) | creates a `TCPInterface` (`FUN_00ab6ce0`), then `TCPInterface::Listen(port, 0x40, 0, -99999)` |
| `0x00ad3c40` | **`TCPInterface::Listen`** (`TCPInterface.cpp`) | `socket(AF_INET,SOCK_STREAM)` → `bind(htons(port), INADDR_ANY)` → `listen(backlog=0x40=64)` → spawns accept thread (`FUN_00ac2540(FUN_00ad5000, this, …)`). `-99999`/`0xfffe7961` = "no thread affinity" sentinel |

### Parser vtable (per command registry)

| Slot | Method |
|---|---|
| `+0x04` | `GetName()` |
| `+0x08` / `+0x0c` | on-connect / on-disconnect greeting hooks |
| `+0x10` | `PrintHelp()` |
| `+0x14` | `ExecuteCommand(cmd, argc, argv, conn, *out, *outlen, rawline)` |
| `+0x1c` | `FindCommand(name, &outCmd)` → bool (out carries param-count at `+0x10`) |
| `+0x20` | `ListCommands()` |

Param count is fixed, or **variable** when it equals sentinel `DAT_01040ef7`. Mismatch ⇒ `"Invalid parameter count."`.

### Connection/transport object vtable (passed as `param_1` to dispatch)

| Slot | Method |
|---|---|
| `+0x0c` | formatted send to client (`printf`-style) |
| `+0x10` | close connection (`"Goodbye!"` on quit) |
| `+0x14` | get next pending command/connection |
| `+0x18` | pop/free handled command |
| `+0x1c` / `+0x20` | enumerate connects/disconnects this tick |

---

## Built-in commands & parsers observed

- **`help`**, **`help <parser>`**, **`help <command>`**, **`quit`** (server builtins).
- **Console password** parser: change / get / remove (`0103c348`/`0103c380`/`0103c3b0`), over the "secure connection".
- **`editor`** cheats (`010001b0`): `load`, `save [<instance>]`, `rename`, `zcorp`, `validate [<keyFilter>]`, `dumpPalettes`, `dumpGeomInfo`, `skinPaintValues` → dumps land in `UserData\Debug`.
- **`prop`** / AppProperties (`0100cec8`): get/set property, `-desc -remove -toVar -fromVar -list -edit -write`; operates on `AppProperties` (or a named list). Ties to `SparkAppProperties`/`SparkProperties`.
- **Light/model** dumps (`00fde550`/`00fde594`/`00fde64c`).

---

## Implications for ReCap

1. **`ConsoleClient` = a telnet client**, not a special binary. Line-based telnet: send `command args`, optionally `<parser> command args`; read text back. `help`/`quit` are free.
2. The **server (`ConsoleServer` + `TelnetTransport`) is present in the retail client** — so the telnet command console is at least partially compiled in. This refines the foehammer testimony that the local CLI server was "probably compiled out" (see [`ARCHITECTURE_OPEN_QUESTIONS.md`](../planning/ARCHITECTURE_OPEN_QUESTIONS.md) Q14, [`DEV_TESTIMONY_FOEHAMMER.md`](DEV_TESTIMONY_FOEHAMMER.md) §4): the *console server* survived; what was likely stripped is a headless **server-only** build.
3. To drive cheats/automation we do **not** need to reimplement a bespoke debug server — options: (a) connect a telnet client to the client's `ConsoleServer` if it can be made to listen; (b) reimplement the parser-registry + telnet listener server-side in ReCap if we want our own command surface.
4. The console port is a **runtime argument** threaded `SetTransport(transport, port)` → `Open(port)` → `bind(htons(port))`. No literal/default appears in the binary; the value comes from boot wiring behind virtual dispatch, consistent with an AppProperty (property names are FNV-hashed → no readable string). The transport binds `INADDR_ANY` with backlog 64 and runs an accept thread — a textbook TCP telnet listener.

## Resolved by RE (this pass)

- ✅ **It is a real TCP listener** — `TCPInterface::Listen` (`0x00ad3c40`, `TCPInterface.cpp`): `socket(AF_INET, SOCK_STREAM)`, `bind` to `htons(port)` on `INADDR_ANY`, `listen(backlog=64)`, dedicated accept thread. Line-based telnet with escape handling (up-arrow history `ESC[A`, backspace) in `0x00ab6530`.
- ✅ **Port path** = `ConsoleServer::SetTransport(transport, port)` (`0x00aad990`) → `TelnetTransport::Open(port)` (`0x00ab6310`) → `TCPInterface::Listen(port,…)`. Port is a `u_short` argument, not a constant.
- ✅ **TelnetTransport is reflectively constructed** (object factory `CreateObject @ 0x00a97bc0`), so it is wired by class-id/config, reinforcing that activation + port are data-driven.

## Still open (need dynamic analysis or the game's propfile)

- ❓ **Exact default port.** Static RE can't recover it (no literal/string; behind vtable wiring + FNV-hashed property). Get it by: (a) debugger breakpoint on `bind`/`0x00ab6310`/`0x00aad990` with the game running; or (b) reading the property config/propfile. ReCap can simply pick its own port anyway.
- ❓ **Whether retail binds/listens by default.** The full listener stack is compiled in, but the call to `SetTransport` is reached through indirect wiring (likely a dev/cheat AppProperty or build flag). Unproven statically.
- ❓ Full parser list (only `editor`, `prop`, password, light/model dumps surfaced so far).

---

## Client-side activation in retail (RE) — how a dev reached it

**foehammer (original dev) said he used an *external local* console "via host or web."** That maps exactly to the two SP_App services we found:
- **"host"** = the `ConsoleServer` + `TelnetTransport` (telnet to a local host:port).
- **"web"** = the `Spark HTTP Server` (`SP_App/HTTPServer`, port 8088) — a browser/HTTP console.

**Both are compiled into retail but NOT started in the normal boot path:**
- App init = `FUN_00538090` (`App::Init`): constructs the `SP_App` service (`FUN_007eda60` → `FUN_007ec840`) and calls many of its vtable methods, but **never invokes the HTTP-setup method** (`FUN_007eb780`, at SP_App vtable **+0x90**) nor wires the telnet `ConsoleServer::SetTransport`. The Spark HTTP object's vtable is at `0x0100c558` (setup ptr at `0x0100c5e8` = +0x90).
- So retail builds the console plumbing but leaves it dormant. foehammer's build evidently triggered it (dev switch/property), or it was a different build config.

**Dormant — HIGH CONFIDENCE (deeper RE 2026-05-27):** the HTTP-setup `FUN_007eb780` (SP_App vtable +0x90) has **no real call site** — the only `CALL [reg+0x90]` instructions in the binary (7, all in `FUN_00d92160`/`FUN_00d92730`, ~`0x00d92xxx`) operate on an unrelated parser object, not SP_App. The init method `FUN_007eb970` calls SP_App vtable +0x94/+0x98 but never +0x90; `FUN_007ee530` (+0x98) only *tears down* the HTTP server (field +0x16c) if it already exists. The telnet `ConsoleServer::SetTransport` (`FUN_00aad990`) has zero xrefs. **Net: neither external console's start path is reachable in retail — the start calls were compiled out of the ship build.** The codebase has explicit **dev vs ship build divergence** (`-devDirs` vs `-shipDirs`, flag at SP_App+0x140, in `FUN_007ed2a0`), so foehammer almost certainly used a **dev build** where the start was wired in. **Conclusion: in retail/ship, the external console cannot be started without injection.**

**What IS reachable in retail (confirmed in `App::Init` + SP_App methods):**
- **Command-line switches** (checked via `FUN_00aed790(L"...")`): `headless`, `demo`, `safe`, `noAsserts`, `automation`, `noReplay`, `noLocalReplay`, `patch`, `flock`, `vSync`/`noVSync`, `noPlugins`, `noScripts`, `noMaterialScripts`, `noEffectScripts`, `noDevEffects`, `dumpShaders`/`dumpShadersForPIX`/`dumpFragmentShaders`/`dumpDirectShaders`. (`-headless` = foehammer's headless mode.) A console/server-enable switch was **not** found in the functions examined — it may live in another SP_App method or be an FNV-hashed AppProperty (no readable string).
- **`localCheats.txt`** — read at startup (preprocessed with an `sinclude` directive), plus `SporeAppStates.txt` and `SporeLabs_DDFList.txt`. **`localCheats.txt` is a reachable way to feed cheat/console commands without any console window.**
- **Command/cheat registry** = `FUN_007b3760` (global). Commands register here: `quit`, `ShipLog`, `AppConsole`, etc. Same registry the console front-ends dispatch against.

**To actually interact, in order of effort:**
1. **`localCheats.txt`** — drop commands in this file; loaded at boot. No console UI/port needed. Cheapest reachable vector.
2. **Empirical** — run retail + `Get-NetTCPConnection`/netstat for `Darkspore.exe`; check for a listening console/web port (e.g. 8088). (Community testing suggests it does not listen by default.)
3. **Enable the dormant external console** — find the gating switch/AppProperty that makes `SP_App` call its HTTP-setup (+0x90) / telnet-listen, **or inject** (Detours, the EAWebKit foothold — see `DEV_TESTIMONY_XACKERY.md`) to call it directly, then connect a telnet/web client (the proper `ConsoleClient`).

---

## Recreating the console in ReCap — viability

**Verdict: very viable, and self-contained.** The protocol is plain line-based telnet (ASCII, `\r\n`, quote-delimited args) — any telnet/netcat client speaks it. None of it touches Blaze/RakNet/asset internals, so it can be built as an independent adapter.

Two scopes:

1. **Minimal (recommended first):** a `Adapters/Console/` TCP adapter that mirrors `ConsoleServer::ProcessCommand`:
   - `TcpListener` on a configurable port (ReCap picks its own; the retail default is irrelevant to us).
   - On accept: send `"Connected to remote command console.\r\nType 'help' for help.\r\n"`.
   - Read lines, tokenize on space with `"` toggling (port `FUN_00ad20a0` semantics).
   - Builtins `help` / `help <parser>` / `help <command>` / `quit`.
   - A `IConsoleParser { Name; Help(); ListCommands(); TryFind(name, out cmd); Execute(cmd, args, write); }` registry, dispatch by parser name or 1-based number prefix; fixed/variable arg-count check.
   - This reproduces the original's UX 1:1 and is ~one adapter file + a parser interface.

2. **Faithful (optional):** also expose ReCap-side commands as parsers — e.g. a `game` parser (spawn, set state, beam), a `prop` parser over server config, a `player` parser. This is where it becomes a genuine debug/automation tool for ReCap, replacing the "recreate a whole debug server" effort the original required.

**What we do NOT need:** the UTFWin in-game console UI, the console-password parser (drop or stub), `PacketConsoleLogger`, the reflective object factory, or the exact retail port. Those are client-side concerns.

**Auth note:** the original gated writes behind a console password over a "secure connection." For ReCap, bind to `127.0.0.1` by default (loopback-only) instead of reimplementing the password scheme.
