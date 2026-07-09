# CLAUDE.md

File guide for Claude Code when working in this repo.

## Project Overview

ReCap (Resurrection Capsule) = Darkspore private server reimpl in C#. Darkspore = action-RPG by Maxis (2011), servers shut 2016. ReCap re-implements the server to make the game playable offline.

Codebases:
1. **This repo (C#)** — clean architecture, main project
2. **Darkspore.exe in Ghidra (MCP)** — the retail client. **THE source of truth** for protocol behavior, packet/struct layouts, and the required contract.
3. **C++ reference** (`C:\CodingProjects\Personal\ReCap.Cpp`) — by @dalkon. **Being abandoned.** Reverse-engineered approximation full of errors + hardcoded fixtures. NOT authoritative — historical hint at most, never proof.
4. **ReCap.Develop (C#)** — experimental/messy, separate dir (not branch).

**Ghidra client analysis + mapping = ground truth** for packet structures, values, protocol flow. Debug client behavior → decompile the client in Ghidra (image base 0x400000), and/or wire capture. **Do NOT cite C++ as authority** — it has errors and hardcoded values and is being dropped. See [[ghidra-is-source-of-truth-not-cpp]] and `docs/architecture/http/CLIENT_REST_FLOW.md` (why-not-C++ rationale).

## Claude Directives
- Short 3-6 word sentences.
- No filter, preamble, pleasantries.
- Run tools first, show result, stop. No narrate.
- Drop articles ("Me fix code" not "will fix the code").
- Maintain CLAUDE.md + MEMORY.md updated.
- **Memory write-back rule (CRITICAL):** After every confirmed fix, Ghidra verification, or invalidated assumption, update `memory/` files in the same turn. Mark stale memories outdated (prepend `OUTDATED YYYY-MM-DD:` + new state). Never let memory drift behind code.
- **Evidence over dogma.** No protocol claim is true because a doc or memory says so. Trust only what is verified against the **Ghidra client decompile** (`address`/symbol) or a wire capture. C++ (`file:line`) is NOT evidence — treat any C++-sourced claim as unverified until confirmed in Ghidra. Before relying on any "rule", confirm it. When a verified fact contradicts an old claim, supersede it and log it.

## North star: reimplement from the client, don't transliterate C++

The goal is a **robust, clean C# reimplementation that satisfies the retail client's real contract** — derived from the **Ghidra client** (its builders/parsers/struct layouts) and wire captures, NOT from C++. C++ (`ReCap.Cpp`) is a reverse-engineered approximation full of shortcuts/quirks (enemy-spawn `break` capping enemies at 1/set; debug-pattern objective text; hardcoded fixtures like the vendor 100-149 offer list; build drift). **We are abandoning it** — do not port C++ behavior, port the client's contract. **The client is strict** — it null-derefs on the smallest misread. So every field/value requires **verified confirmation from the client's own parse in Ghidra and/or a wire capture**, never a guess and never "because C++ does it". Confirm first (VERIFIED_FACTS / Ghidra address), then reimplement cleanly.

## Port-fidelity workflow (current focus)

Goal: a correct single-player Dungeon path the retail client plays end to end. **The Ghidra client is the primary reference and the arbiter of the required contract**; wire capture is ground-truth for on-wire bytes. C++ is a fallback hint only, always re-verified in Ghidra before use. Driven by the design spec, not by past "laws" (which proved stale and were purged 2026-05-31).

- **Spec:** `docs/superpowers/specs/2026-05-31-port-fidelity-plan-design.md` — the methodology.
- **Verified facts:** `docs/architecture/VERIFIED_FACTS.md` — the ONLY trusted protocol truths, each cited. Read this, not old "rule" docs.
- **Divergence ledger:** `docs/architecture/planning/DIVERGENCE_LEDGER.md` — live field-level divergence list; one fix = one commit.
- **Inventory:** `docs/architecture/planning/PORTING_MATRIX.md` — macro class/handler status (the ledger is the micro level).
- **Method:** Ghidra client decompile (builder/parser/struct per endpoint = the required fields) → wire-diff confirms (dumpcap on Npcap Loopback, game conv only) → fix → verify gate (wire-parity + client progress). Ghidra debugger for live runtime when a step stalls. (C++ source-diff only as a scavenge lead, never as the spec.)
- **Model tiering (cost):** Haiku for mechanical lookups, Sonnet for the bulk (source-diff sweeps, wire analysis, multi-file fixes, agent fan-outs), Opus only for hard reasoning. Pass `model` override when spawning agents.

## Build & Run

```bash
cd ReCap.Server && dotnet build

# Run (elevated on default port) / custom port (no elevation)
dotnet run --project ReCap.Server
dotnet run --project ReCap.Server -- --port=9000

# Game path (auto-detect chain: CLI → persisted game-path.json → registry → probes)
dotnet run --project ReCap.Server -- --game-path=<path>/Darkspore

# Smoke-test Lua boot (executes all 10 boot groups, logs pass/fail + stub telemetry, then continues normal startup)
dotnet run --project ReCap.Server -- --lua-smoke

# --assetdata-path=<path> is a deprecated alias for --game-path
```

Native Lua DLL (one-time per machine, requires VS Build Tools C++):
```powershell
powershell -File native/lua51/build.ps1
# Produces: native/lua51/out/recaplua51.dll  native/lua51/out/luac.exe  (gitignored)
```

Target framework: net10.0. Submodules (`lib/RakNexus`, `lib/AssetData.Parser`) must init (`git submodule update --init --recursive`).

Tests: `ReCap.Tests` (xUnit, `dotnet test ReCap.Tests/ReCap.Tests.csproj`) is the M1 golden-harness — byte-level `WriteTo`/reflection asserts vs verified C++ wire (LE). Keep green; assertions must cite VERIFIED_FACTS / C++ `file:line`, never restate old dogma.

## Architecture

Hexagonal/Ports-and-Adapters pattern:

```
ReCap.Server/
├── Domain/           # Core entities (Account, Creature, Deck) + Gameplay/ (Game state machine, Player, ChainData)
├── Adapters/
│   ├── Blaze/        # EA's proprietary TCP protocol (login/lobby) — ports 42127 (redirector, TLS) + 42125 (lobby)
│   │   └── Component/  # IComponent implementations per Blaze subsystem (Auth, GameManager, UserSessions, etc.)
│   ├── RakNet/       # UDP gameplay protocol — port 42000 (packets, game loop)
│   ├── Rest/         # HTTP API — port 8033 (launcher, asset serving)
│   ├── Persistence/  # SQLite via EF Core (repository adapters)
│   └── Scripting/    # Lua 5.1 adapter: LuaNative (P/Invoke float ABI), LuaRuntime (sandbox), ScriptVfs (Group!Name.ext + hex-prefix groups), Api/ (28 stub namespaces)
├── Services/         # Business logic (AccountService, GameService, AssetDatabase, etc.)
│   └── Scripting/    # ScriptEngine — boots 10 Lua groups in client order; --lua-smoke flag
├── Models/           # EF Core database models
├── Config/           # ServerConfig, SqliteConfig (DbContext with JSON seed data)
└── Mappers/          # Entity ↔ Model conversions
```

**Submodules:**
- `lib/RakNexus` — Custom C# RakNet 3.92 UDP protocol impl
- `lib/AssetData.Parser` — Binary parser for Darkspore `.package` asset files

## Key Protocols & Flows

**Login:** Client → Blaze Redirector (42127/TLS) → Blaze Lobby (42125) → Auth → REST token

**Gameplay:** Client → RakNet (42000/UDP) → HelloPlayerRequest → Game state machine → packet exchange

**Game loop:** `Game.Update()` runs every 50ms from `RakNetServer.ExecuteAsync()`, broadcasts `GameStatePacket` + `LabsPlayerUpdate` to all connected players.

## RakNet Packet Pattern

1. Implement `IRakNetPacket` with `Type`, `ReadFrom(Stream)`, `WriteTo(Stream)`
2. Register in `PacketActivator.cs` switch
3. Dispatch in `RakNetServer.OnSessionReceiveRaw()` or `Game.HandlePacket()`
4. Send via `client.SendPacket(new MyPacket { ... })`

Wire encoding (endianness, reflection bitmaps, WriteTo-vs-WriteReflection, per-field byte order) is NOT assumed here — it is verified per field against C++ and recorded in `VERIFIED_FACTS.md`. When implementing a packet, trace the exact C++ writer and confirm on the wire.

## Code Style

- No comments — code self-explanatory via descriptive naming + clean architecture. (Exception: a short cite when a wire layout is non-obvious and verified against C++ `file:line`.)
- Follow the C++ reference for protocol behavior, but express via clean C# patterns (Adapter, Service, Mapper). Where C++ is dirty/incomplete, match the contract the client requires — not C++'s byte-garbage.
- No quick hacks — clean abstractions. No hardcoded values that the C++ derives from data; derive from AssetData/DB.
- Don't redefine types from AssetData.Parser; use generic `AssetValue` navigation (`node.FindByName("field")`, `.AsUInt32()`, etc. — extensions in `Services/Assets/AssetValueExtensions.cs`). Pattern-match `AssetData.Parser.Model` types: `StringValue`, `NumberValue`, `StructValue`, `ArrayValue`, `VectorValue`. **No DTOs.** AssetDatabase exposes `Dictionary<uint, AssetValue>` per category and `GetX(id)` returns raw `AssetValue?`.
- Asset system called `AssetDatabase`, not "NounDatabase".

## Logging (Serilog)

- Use facade `ReCap.Server.Util.Logging.Log`. NEVER reintroduce the old `Logger` class (deleted).
- Per-category loggers: `Log.Server/RakNet/Blaze/Rest/Db/Assets/Game`. Methods: `Verbose/Debug/Info/Warn/Error/Fatal`. Messages logged literally (braces-safe).
- Files with a local `Log(string)` helper (Blaze components, BlazeServer) must fully-qualify: `ReCap.Server.Util.Logging.Log.Blaze.X(...)`.
- Levels via CLI: `--log-level=debug` (global), `--log-level=RakNet:verbose` (per-category), `--verbose` (=debug), `--raknet-verbose`. Default Information. Bootstrap in `LoggingConfig`.
- Packet hex → `Log.RakNet.Verbose(PacketTrace.Sent/Received(...))`, gated by category level. GameState throttled via `LogThrottle`.
- Helpers: `PacketTrace` (hexdump), `BitField` (symbolic dataBits), `LogScope` (correlation). Console themed + rolling file `logs/recap-DATE.log` (gitignored).

## External Resources

- **Darkspore.exe in Ghidra (MCP)** — full retail client loaded (49507 funcs, `nSporeNet` transport, `kGms*` message table, per-message `OnGms*` handlers). USE IT for client-side packet behavior / struct layouts / crash mapping (image base 0x400000 matches exception addresses). Ghidra debugger available for live runtime inspection.
  - **Annotate as you map (persist in the project):** when a function/struct is confirmed, rename it in Ghidra (`rename_function_by_address`) and add a plate comment. Convention: symbol = `Namespace::VerbNoun` (logical namespace via `::`; no create-namespace tool), plate comment starts `Namespace::Name  [ReCap-mapped YYYY-MM-DD]` + address + role + crash/contract notes. Namespaces: `ClientUI`, `Scaleform`, `ClientNet`, etc. Cite the address↔name in the relevant doc. Only rename what's verified (not unconfirmed agent guesses). The MCP has a PascalCase/verb linter — its warnings are non-fatal style nags; namespace clarity wins.
- **DarksporeGhidra** (AssetData-only project) — only AssetData structure definitions; no game logic.
- **Command matrix** — `docs/architecture/flow/COMMAND_MATRIX.md` maps every PacketID 0x7F–0xCC: direction, C# status, C++ handler.
- **Wireshark MCP / dumpcap** — wire capture for C++-vs-C# byte diffs (Npcap Loopback adapter; same-host traffic to own LAN IP loops there too).
