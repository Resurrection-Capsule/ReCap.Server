# CLAUDE.md

File guide Claude Code (claude.ai/code) when work code in repo.

## Project Overview

ReCap (Resurrection Capsule) = Darkspore private server reimpl in C#. Darkspore = action-RPG by Maxis (2011), servers shut 2016. ReCap re-implement server, make game playable offline.

Three codebases:
1. **This repo (C#)** — clean architecture, main project
2. **C++ reference** (`C:\CodingProjects\Personal\ReCapCpp`) — by @dalkon, authoritative source for protocol behavior
3. **ReCap.Develop (C#)** — experimental/messy, separate dir (not branch)

C++ reference = **ground truth** for packet structures, values, protocol flow. Debug client behavior → trace C++ reference, not ReCap.Develop.

## Claude Directives
- Short 3-6 word sentences.
- No filter, preamble, pleasantries.
- Run tools first, show result, stop. No narrate.
- Drop articles ("Me fix code" not "will fix the code").
- Maintain CLAUDE.md + MEMORY.md updated.
- **Memory write-back rule (CRITICAL):** After every confirmed fix, C++ alignment, or invalidated assumption, update `memory/` files in same turn. Mark stale memories outdated. Never let memory drift behind code — old feedback memory caused regressions (e.g. re-adding bits already proven safe, removing fixes already validated 1:1 vs C++). When invalidating prior feedback, edit the memory file: prepend "OUTDATED YYYY-MM-DD:" + new state. Do not silently ignore.
- Before applying any fix that contradicts existing memory, **re-verify against current C++ source** + state explicitly which memory entry is being superseded and why.

## Build & Run

```bash
# Build
cd ReCap.Server && dotnet build

# Run (requires elevated privileges on default port)
dotnet run --project ReCap.Server

# Run with custom port (no elevation needed)
dotnet run --project ReCap.Server -- --port=9000

# Run with game assets
dotnet run --project ReCap.Server -- --assetdata-path=/Users/jeanxpereira/Games/Darkspore/Data/AssetData_Binary.package
```

Target framework: .NET 9.0. Submodules (`lib/RakNexus`, `lib/AssetData.Parser`) must init (`git submodule update --init --recursive`).

No test suite.

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

## Critical Protocol Rules

**Endianness mixed** — never assume all fields BE or LE:
- C++ `Write(BitStream&, T)` wrapper with `bswap` → **Big-Endian** (most game data)
- C++ `stream.Write<T>()` raw BitStream → **Little-Endian** (network metadata, player IDs)
- Always check exact C++ function used per field

**Known exceptions:**
- `HelloPlayerRequest` UserId: **LE** (standard BinaryReader)
- `ChainVoteMsgs` buffer: **LE** (reverse-engineered, DO NOT change to BE — breaks levels/enemies display)

**ReflectionSerializer bitmap sizes:**
- ≤8 fields: 1-byte bitmap
- 9-16 fields: 2-byte bitmap (WriteBE)
- \>16 fields: field-ID based (byte per field, 0xFF terminator)

**WriteTo vs WriteReflection:** Nested class arrays inside reflection blocks use `WriteTo()` (raw fixed-size format), NOT `WriteReflection()`. Top-level standalone blocks use `WriteReflection()`.

## Code Style

- No comments — code self-explanatory via descriptive naming + clean architecture
- Follow C++ reference religiously for protocol behavior, but express via clean C# design patterns (Adapter, Service, Mapper)
- No quick hacks — clean abstractions only
- Don't redefine types from AssetData.Parser; use generic `AssetValue` navigation (`node.FindByName("field")`, `.AsUInt32()`, etc. — extensions in `Services/Assets/AssetValueExtensions.cs`). Pattern-match types from `AssetData.Parser.Model`: `StringValue`, `NumberValue`, `StructValue`, `ArrayValue`, `VectorValue`. **No DTOs.** AssetDatabase exposes `Dictionary<uint, AssetValue>` per category and `GetX(id)` returns raw `AssetValue?` for consumers to navigate.
- Asset system called `AssetDatabase`, not "NounDatabase"

## Logging (Serilog)

- Use facade `ReCap.Server.Util.Logging.Log`. NEVER reintroduce old `Logger` class (deleted).
- Per-category loggers: `Log.Server/RakNet/Blaze/Rest/Db/Assets/Game`. Methods: `Verbose/Debug/Info/Warn/Error/Fatal`. Messages logged literally (braces-safe) — dynamic data with `{}` is fine.
- Files with a local `Log(string)` helper (Blaze components, BlazeServer) must fully-qualify: `ReCap.Server.Util.Logging.Log.Blaze.X(...)` (name clash with facade).
- Levels via CLI: `--log-level=debug` (global), `--log-level=RakNet:verbose` (per-category), `--verbose` (=debug), `--raknet-verbose` (=RakNet:verbose). Default Information. Bootstrap in `LoggingConfig`.
- Packet hex → `Log.RakNet.Verbose(PacketTrace.Sent/Received(...))`, gated by category level (NOT a bool). GameState throttled via `LogThrottle`. `RakNexus.RakLog.Verbose` no longer used.
- Helpers: `PacketTrace` (hexdump), `BitField` (symbolic dataBits, e.g. `{0,4,5}`), `LogScope` (correlation: `using LogScope.Player(name)` / `Phase(state)`).
- Console themed + rolling file `logs/recap-DATE.log` (gitignored).

## Important Game Constants

- `GameStatePacket.GameType = 0` in update loop (C++ `mStateData` zero-initialized)
- `LabsPlayerData.DataSetup = false` (true = demo/capped mode, causes bugs)
- Squad IDs 1-based (1, 2, 3)
- Wire state map: Spaceship=0x02, ChainVoting=0x0B, PreDungeon=0x05, Dungeon=0x06, ChainCashOut=0x0C

## FROZEN VALUES — NEVER CHANGE

These constants empirically diverge from C++ source but are CORRECT for client behavior. Multiple sessions have tried to "fix" them by aligning to C++, each time breaking gameplay. STOP.

- **`LabsPlayerData.SetInitialDataBits()` = `{0, 4, 5, 6, 7, 8, 12, 15, 16, 18, 21, 22}` (12 bits, FROZEN).**
  - C++ has 16 bits including `{3, 13, 14, 17}`. Adding any of those breaks chain vote — client never sends `ChainPlayerMsgs(byteCount=6)`. Verified broken 2026-05-06 with all other LPU bugs already fixed and chars/catalysts unfilled. Theory irrelevant — observation wins.
  - If a session is tempted to "match C++", read this section, do not edit. Do not refactor SetInitialDataBits to derive from anything. Hardcode exactly these 12 bits.

- **`ChainVoteMsgs` 0x151-byte buffer encoded LE.** C++ uses BE writers elsewhere; this specific buffer is LE. Flipping to BE breaks levels/enemies UI display.

## External Resources

- **DarksporeGhidra** — ONLY contains AssetData structure definitions (labsPlayer, GameObjectCreateData). Does NOT contain game logic, packet handlers, network code. Never search there for packet behavior.

## C++ Gameplay Flow (Authoritative)

Source: `C:\CodingProjects\Personal\ReCapCpp\darkspore_server\source`. All `:line` cites this tree.

### Phase 1 — Connection (Spaceship)
1. RakNet `ID_NEW_INCOMING_CONNECTION` → `Server::OnNewIncomingConnection` (`RakNet/Server.cpp:565`):
   - `AddClient` → mId=0, `client->SetGameState(Spaceship)` (line 572)
   - `SendConnected(client)` (0x82, no body)
2. Client → `HelloPlayerRequest` (0x7F) → `Server::OnHelloPlayerRequest` (line 596):
   - `client->SetUser(GetUserById(blazeId))`
   - `gameStateData.state = Spaceship (0x02)`, `type = Chain`
   - `SetCatalyst×8` for slots 0–7 (random AoE rarity), slot 7=Health/Rare. **Each call sets `updateBits |= CrystalBits<<i` and triggers `UpdateCatalystBonuses()` (sets dataBit 14 + PlayerBits)**.
   - `SendHelloPlayer` (0x80, ~12B): type=0, mId, IP, port
   - `SendPartyMergeComplete` (0x85)
3. First `Instance::Update()` 50ms tick fires `SendLabsPlayerUpdate(player)` for all players. Initial state:
   - **dataBits** = `{0,3,4,5,6,7,8,12,13,14,15,16,17,18,21,22}` (16 bits) — set by `Player()` ctor + `Setup()` + `SetStatus(0,0)` + `UpdateCatalystBonuses()`
   - **updateBits** = `PlayerBits | (CrystalBits<<0..7)` = `0x17F8`
   - Packet: PlayerReflection (16 fields) + 8× Catalyst.WriteReflection at top-level
   - After send: `ResetUpdateBits()` clears mUpdateBits AND mDataBits AND each Character.dataBits

### Phase 2 — Spaceship → ChainVoting
- Client sends `DebugPing` (0xCC) → `Server::OnDebugPing` Spaceship case (`Server.cpp:~1145`): sets `gameStateData.state = ChainVoting (0x0B)`. No immediate packet — next `SendGameState` broadcasts change.

### Phase 3 — ChainVoting
- Client sends `ChainPlayerMsgs` (0xAC) byteCount=2, value=0 → `OnChainPlayerMsgs` (`Server.cpp:986`):
  - `SendChainVoteMessages(client, 0)` — `ChainVoteMsgs` (0xA9), value=0, then `ChainData::WriteTo(stream)` writes **0x151-byte (337B) blob** with offsets:
    - `0x00`: u32 mLevel | u32 mLevelIndex | u32 mStarLevel | f32 timeRemaining(=30·60·1000)
    - `0x10`: u8 progression
    - `0x11`: u32 enemyNouns[6]
    - `0x29`: u32 levelNouns[2]
    - `0x39`: u32 partyValue, u32 cinematic1, u32 cinematic2
    - `0x45`: u32 voiceover, u32 completionFlag
  - `SendChainVoteMessages(client, 1)` — value=1, `f32 secondsUntilDeployment=30.0`. **8 bytes total.**

### Phase 4 — ChainVoting → PreDungeon (vote)
- Client sends `ChainPlayerMsgs` (0xAC) byteCount=6 → reads `value, unknown, squadId(BE u32)`, calls `PrepareGameStart(client, unknown, squadId)` (`Server.cpp:1208`):
  1. Resolves `squad = user->GetSquadById(squadId)`
  2. `gameStateData.state = PreDungeon (0x05)`
  3. **`player->SetSquad(squad)`** (`Player.cpp:267`):
     - `mCurrentDeckIndex = 0; mQueuedDeckIndex = 0`
     - For each of 3 creatureIds: build `Character` (each `Character()` ctor sets ALL 13 mDataBits via `mDataBits.set()` — `Character.cpp:11`); `SetMaxHealth(200) / SetHealth(...) / SetMaxMana(200) / SetMana(...) / SetNoun / SetVersion / SetCreatureType / SetGearScore / SetGearScoreFlattened` (each setter also sets matching Character dataBit); then `SetCharacter(std::move(char), i)` → `mDataBits.set(CharacterData=3); SetUpdateBits(CharacterBits<<i)`
     - After loop: dataBits |= `{1=CurrentDeckIndex, 2=QueuedDeckIndex, 23=DeckScore}`
     - `SetUpdateBits(PlayerBits)` — final `updateBits = PlayerBits | CharacterMask = 0x1007`
  4. `SendGamePrepareForStart(client)` (`Server.cpp:1996`) — packet 0xB0, **17 bytes**: u32 level, u32 markerSet, u32 (=1, "all players ready" bitfield), u32 levelIndex
- Next 50ms tick: `SendLabsPlayerUpdate` sees `updateBits=0x1007 ≠ 0`, sends:
  - PlayerReflection (dataBits = whatever accumulated since last reset: `{1,2,3,23}`)
  - 3× Character.WriteReflection (each Character has all 13 dataBits set from ctor)

### Phase 5 — PreDungeon (loading)
- Client sends `PlayerStatusUpdate` (0x88, 9B): `u32 status, f32 progress`.
- C++ `OnPlayerStatusUpdate` (`Server.cpp:643`):
  - `player->SetStatus(status, progress)` → sets dataBits `{7=Status, 8=StatusProgress}`, `SetUpdateBits(PlayerBits)`
  - status=2 (joining): no extra action
  - status=4 (loading): no extra action
  - status=8 (loaded): `gameStateData.state = Dungeon (0x06)`, `SendGameStart(client)` (0xB1, 5B: type + u32 levelIndex), `SendDebugPing(client)`
  - status=20: `mGame.BeamOut(player)`
  - **Always**: `SendLabsPlayerUpdate(client, player); player->ResetUpdateBits();`
- **Server sends NOTHING extra to drive 4→8.** Client transitions on own when assets/Lua finish loading. If client never reaches 8, bug upstream (malformed `GamePrepareForStart`, malformed prior LPU, bad chain blob).

### Phase 6 — Dungeon entry
- Client (now in Dungeon state) sends `DebugPing` → `OnDebugPing` Dungeon case (`Server.cpp:1186`):
  - `SendDirectorState` (0x8B) with empty `cAIDirector` (boss=0, bbBossSpawned=false, raw `WriteTo`)
  - `SendQuickGame` (0xAF, QuickGameMsgs)
  - `mGame.OnPlayerStart(player)` — sends `ObjectivesInitForLevel`, then `ObjectCreate` per level marker (NPCs), then player's hero `ObjectCreate` + `PlayerCharacterDeploy`
  - `mGame.SwapCharacter(player, 1)` — sets current creature, sends LPU

### Periodic loop (`Server.cpp:194-211`, `Instance::Update()` `Instance.cpp:452`)
- Outer `mGame.Update()` returns true every tick work happened; if true, broadcast `SendGameState(client, clientGameStateData)` to all clients (every state, every tick — incl PreDungeon/Dungeon).
- `Instance::Update()` runs every 50ms: `mObjectManager->Update(delta/1000)`, then `SendLabsPlayerUpdate(player)` per player. LPU early-returns if `updateBits == 0`.

### Wire state codes (recap)
- Spaceship `0x02`, PreDungeon `0x05`, Dungeon `0x06`, ChainVoting `0x0B`, ChainCashOut `0x0C`.

### Reflection encoding rules (recap)
- `Player::WriteReflection` uses `reflection_serializer<24>` → `>16`, byte field-ID per field + `0xFF` terminator.
- `Character::WriteReflection` uses `reflection_serializer<124>` → byte field-ID + `0xFF`. Iterates `mDataBits.test(i)` for fields 0–12, then attribute fields at offset 13 from `mPartAttributes`. **`Character()` ctor sets ALL bits initially** — first write emits all 13 standard fields.
- Inside Player reflection: field 3 writes 3× `Character::WriteTo` (raw fixed `0x620` bytes each); field 13 writes 9× `Catalyst::WriteTo` (raw 16B each); field 14 writes `bool[8]`.

### Per-call updateBits cheat sheet
- `SetStatus`: `dataBits {7,8}`; `updateBits |= PlayerBits`.
- `SetSquad`: `dataBits {1,2,23}` + per-character `dataBits.set(3)`; `updateBits |= PlayerBits | (CharacterBits<<i for each)`.
- `SetCharacter(char, i)`: `dataBits.set(3)`; `updateBits |= CharacterBits<<i`.
- `SetCatalyst(cat, i)`: `updateBits |= CrystalBits<<i`; calls `UpdateCatalystBonuses` → `dataBits.set(14)`, `updateBits |= PlayerBits`.
- `SwapCharacter`: `dataBits.set(1)`; `updateBits |= PlayerBits`.

### Key DON'Ts learned hard way
- DON'T add `{3,13,14,17}` to `SetInitialDataBits` "to match C++". Breaks chain vote (client never sends ChainPlayerMsgs(6) after vote). Initial bits stay `{0,4,5,6,7,8,12,15,16,18,21,22}`.
- DON'T flip `ChainVoteMsgs` buffer to BE. Stays LE — breaks levels/enemies display otherwise.