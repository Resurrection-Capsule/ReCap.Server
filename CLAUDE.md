# CLAUDE.md

File guide for Claude Code when working in this repo.

## Project Overview

ReCap (Resurrection Capsule) = Darkspore private server reimpl in C#. Darkspore = action-RPG by Maxis (2011), servers shut 2016. ReCap re-implements the server to make the game playable offline.

Three codebases:
1. **This repo (C#)** — clean architecture, main project
2. **C++ reference** (`C:\CodingProjects\Personal\ReCapCpp`) — by @dalkon, authoritative source for protocol behavior
3. **ReCap.Develop (C#)** — experimental/messy, separate dir (not branch)

C++ reference = **ground truth** for packet structures, values, protocol flow. Debug client behavior → trace the C++ reference, not ReCap.Develop.

## Claude Directives
- Short 3-6 word sentences.
- No filter, preamble, pleasantries.
- Run tools first, show result, stop. No narrate.
- Drop articles ("Me fix code" not "will fix the code").
- Maintain CLAUDE.md + MEMORY.md updated.
- **Memory write-back rule (CRITICAL):** After every confirmed fix, C++ alignment, or invalidated assumption, update `memory/` files in the same turn. Mark stale memories outdated (prepend `OUTDATED YYYY-MM-DD:` + new state). Never let memory drift behind code.
- **Evidence over dogma.** No protocol claim is true because a doc or memory says so. Trust only what is verified against C++ source (`file:line`) or a wire capture. Before relying on any "rule", confirm it. When a verified fact contradicts an old claim, supersede it and log it.

## Port-fidelity workflow (current focus)

Goal: 1:1 byte-level fidelity vs C++ for the single-player Dungeon path. Driven by the design spec, not by past "laws" (which proved stale and were purged 2026-05-31).

- **Spec:** `docs/superpowers/specs/2026-05-31-port-fidelity-plan-design.md` — the methodology.
- **Verified facts:** `docs/architecture/VERIFIED_FACTS.md` — the ONLY trusted protocol truths, each cited. Read this, not old "rule" docs.
- **Divergence ledger:** `docs/architecture/planning/DIVERGENCE_LEDGER.md` — live field-level divergence list; one fix = one commit.
- **Inventory:** `docs/architecture/planning/PORTING_MATRIX.md` — macro class/handler status (the ledger is the micro level).
- **Method:** source-diff sweeps (vs C++) → wire-diff confirms (dumpcap on Npcap Loopback, game conv only) → fix → verify gate (wire-parity + client progress). Ghidra debugger only when a step stalls with no obvious wire divergence.
- **Model tiering (cost):** Haiku for mechanical lookups, Sonnet for the bulk (source-diff sweeps, wire analysis, multi-file fixes, agent fan-outs), Opus only for hard reasoning. Pass `model` override when spawning agents.

## Build & Run

```bash
cd ReCap.Server && dotnet build

# Run (elevated on default port) / custom port (no elevation) / with assets
dotnet run --project ReCap.Server
dotnet run --project ReCap.Server -- --port=9000
dotnet run --project ReCap.Server -- --assetdata-path=<path>/AssetData_Binary.package
```

Target framework: .NET 9.0. Submodules (`lib/RakNexus`, `lib/AssetData.Parser`) must init (`git submodule update --init --recursive`).

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
│   └── Persistence/  # SQLite via EF Core (repository adapters)
├── Services/         # Business logic (AccountService, GameService, AssetDatabase, etc.)
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
- **DarksporeGhidra** (AssetData-only project) — only AssetData structure definitions; no game logic.
- **Command matrix** — `docs/architecture/flow/COMMAND_MATRIX.md` maps every PacketID 0x7F–0xCC: direction, C# status, C++ handler.
- **Wireshark MCP / dumpcap** — wire capture for C++-vs-C# byte diffs (Npcap Loopback adapter; same-host traffic to own LAN IP loops there too).
