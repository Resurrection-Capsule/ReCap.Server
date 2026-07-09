# Phase 00 — Boot

Cold start: process up, sockets bound, asset databases warm, ready to accept the first TCP/UDP packet from the client.

```mermaid
sequenceDiagram
    autonumber
    participant OS as OS / kernel
    participant Main as main()
    participant Cfg as Config loader
    participant Asset as Asset / Noun cache
    participant Net as Server adapters
    participant Loop as Event loop

    OS->>Main: argv (--port, --assetdata-path, --database-path)
    Main->>Cfg: parse args + read config.xml / defaults
    Cfg-->>Main: ports, paths, version locked, hostname
    Main->>Asset: kick off asset/noun warm-up (background)
    Main->>Net: construct N adapters (TCP/UDP/HTTP)
    Net-->>OS: bind sockets, start listening
    Main->>Loop: io_service.run() / Task.Run loops
    Asset-->>Loop: ready (eventually)
    Note over Loop: stays here until SIGINT/SIGTERM
```

---

## C++ — `recap_server (darkspore_server)`

### Entry call chain

| Step | Location | What happens |
|---|---|---|
| `main(argc, argv)` | `Main.cpp:303` | Bootstraps `Application::sApplication`. |
| `Application::InitApp` | `Main.cpp:50-85` | Parses CLI args (`--timestamps`, `--version`, `--darkspore-path`, `--help`). Allocates singleton. |
| `Application::OnInit` | `Main.cpp:91-201` | Loads config, builds subsystems and every server, starts asset warm-up thread. Returns `bool`. |
| `Application::Run` | `Main.cpp:219-225` | Calls `mIoService.run()`; this blocks until SIGINT/SIGTERM. |
| `Application::OnExit` | `Main.cpp:203-217` | Resets unique_ptrs in dependency order; shuts down scheduler; drops DBPF cache. |

### Config

- `Game::Config::Load("config.xml")` (`Main.cpp:107`, impl `Game/Config.cpp:30`).
- `ConfigKey` enum: `Game/Config.h:13-32`. 19 keys total.
- Default values seeded if `config.xml` missing (`Game/Config.cpp:82-99`):

| Key | Default | Comment |
|---|---|---|
| `SKIP_LAUNCHER` | `true` | |
| `VERSION_LOCKED` | `false` | |
| `SINGLEPLAYER_ONLY` | `true` | |
| `SERVER_HOST` | `127.0.0.1` | |
| `SERVER_REDIRECTOR_PORT` | `42127` | TLS endpoint. |
| `SERVER_BLAZE_PORT` | `10041` | **Lobby (Blaze main)** — not 42125. The client gets the real lobby port from the redirector response. |
| `SERVER_PSS_PORT` | `8443` | |
| `SERVER_TICK_PORT` | `8999` | |
| `SERVER_TELEMETRY_PORT` | `9988` | |
| `SERVER_QOS_PORT` | `3659` | UDP. |
| `SERVER_HTTP_PORT` | `8033` | |
| `SERVER_HTTP_TELEMETRY_PORT` | `8080` | |
| `SERVER_HTTP_QOS_PORT` | `17502` | |
| `STORAGE_PATH` | `storage/` | |
| `WWW_STATIC_PATH` | `data/www/static/` | |
| `DARKSPORE_INDEX_PAGE_PATH` | `index.html` | |
| `TEMPLATE_CREATURE_PATH` | `data/creature_templates.json` | |
| `TEMPLATE_CREATURE_PARTS_PATH` | `data/creature_parts_templates.json` | |

### Subsystems constructed (order matters)

`Main.cpp:107-152`:

1. `Game::Config::Load` — `Main.cpp:107`
2. `Scheduler` (`mScheduler`) — `Main.cpp:110`
3. `SporeNet::Instance` (`mSporeNet`) — `Main.cpp:113` — owns persistent account/squad state.
4. `Game::API` (`mGameAPI`) — `Main.cpp:116` — registers HTTP routes; setup deferred to step 9.
5. Resolve `ip` via `utils::net::resolve_ip(host, http_port)` — `Main.cpp:129`.
6. Blaze servers — `Main.cpp:132-137`:
   - `mRedirectorServer` :42127
   - `mBlazeServer` :10041 (default)
   - `mPssServer` :8443
   - `mTickServer` :8999
   - `mTelemetryServer` :9988
7. `mQosServer` (UDP) — `Main.cpp:140`.
8. HTTP trio — `Main.cpp:143-149`:
   - `mHttpServer` :8033
   - `mHttpTelemetryServer` :8080
   - `mHttpQosServer` :17502
   - The same `Router` is shared between the three (`Main.cpp:147-149`).
9. `mGameAPI->setup()` — `Main.cpp:152` — populates HTTP routes.
10. Asset warm-up thread (detached) — `Main.cpp:155-198`:
    - `Installer::LoadDarksporeData(install_path, version)` — copies / validates client assets.
    - `Game::NounDatabase::Instance()` — eager singleton init, parses every noun XML.
    - `Game::GlobalLua::Instance().Initialize()` — boots the Lua VM and loads gameplay scripts.

### Threading model

- One `boost::asio::io_context` (`mIoService`, `Main.cpp:46`) shared across **all** Blaze + HTTP + QoS servers.
- `SIGINT/SIGTERM` wired via `mSignals` (`Main.cpp:47`).
- Asset warm-up runs on a single **detached** background `std::thread` (`Main.cpp:155-198`).
- `Application::Run` is the only place `mIoService.run()` is called (`Main.cpp:221`) — the process is single-threaded for network I/O.

### Shutdown order (`OnExit`, `Main.cpp:203-217`)

`mGameAPI` → `mRedirectorServer` → `mBlazeServer` → `mHttpServer` → `mQosServer` → `mSporeNet` → `mScheduler->Shutdown()` → `mScheduler` reset → `DBPFManager::shutdown()`.

> Notably, `mPssServer`, `mTickServer`, `mTelemetryServer`, `mHttpTelemetryServer`, `mHttpQosServer` are **not** explicitly reset — they piggyback on the global destruction order when `sApplication` itself is destroyed.

---

## C# — `ReCap.Server`

### Entry call chain

| Step | Location | What happens |
|---|---|---|
| `static async Task Main(string[] args)` | `Program.cs:27` | Parses `--port`, `--database-path`, `--assetdata-path`, `--help`. |
| Elevation check | `Program.cs:76-85` | If on default privileged port and not elevated, calls `TryRelaunchElevatedAsync`. |
| `ServerConfig.Configure(serverOpts)` | `Program.cs:107` | Snapshots a `ServerConfigOptions` into static `ServerConfig` (`Config/ServerConfig.cs:47`). |
| `new AssetDatabase(...)` | `Program.cs:109-111` | Optional. Only if `--assetdata-path` supplied. |
| `new GameService { Assets = assetDatabase }` | `Program.cs:113` | Shared in-memory game registry. |
| `new SqliteConfig(); dbConfig.Start()` | `Program.cs:118-119` | EF Core `DbContext`; `EnsureCreated` + seed (`Config/SqliteConfig.cs:28`). |
| Redirector via `Task.Run(redirector.Start)` | `Program.cs:126-127` | TLS on :42127. |
| Lobby via `Task.Run(lobby.Start)` | `Program.cs:130-131` | TCP on :42125 (hardcoded). |
| RakNet via `Task.Run(() => raknet.ExecuteAsync(token))` | `Program.cs:134-135` | UDP on :42000 (hardcoded). |
| REST `restClientAdapter.Run()` | `Program.cs:140-143` | Blocking call on the main thread; catches `HttpListenerException` for port-permission failures and retries elevated. |

### Config

`Config/ServerConfig.cs` is a static facade over `ServerConfigOptions` (`Config/ServerConfigOptions.cs`). Defaults:

| Property | Default | Source |
|---|---|---|
| `HostName` | `"localhost"` | `ServerConfigOptions.cs:12` |
| `HostIP` | `127.0.0.1` | `ServerConfigOptions.cs:21` |
| `GameVersion` | `5.3.0.127` | `ServerConfigOptions.cs:30` |
| `ServerDatabaseDirectory` | `AppDomain.CurrentDomain.BaseDirectory` | `ServerConfigOptions.cs:39` |
| `GamePath` | `""` | `ServerConfigOptions.cs:47` |

There is **no config file**. All overrides come from CLI args:

- `--port=<int>` — REST port.
- `--database-path=<dir>` — directory for `server.db`.
- `--assetdata-path=<file>` — path to `AssetData_Binary.package`.

Ports for Redirector / Lobby / RakNet are hardcoded in `Program.cs:126`, `Program.cs:130`, `Program.cs:134`.

### Subsystems constructed (order matters)

`Program.cs:107-140`:

1. `ServerConfig.Configure` — snapshot CLI overrides.
2. `AssetDatabase` (optional) — `Program.cs:109-111`.
3. `GameService` (in-memory game registry) — `Program.cs:113`.
4. `CancellationTokenSource` — `Program.cs:115`.
5. `SqliteConfig.Start()` — `Program.cs:118-119`:
   - `DbContext.Database.EnsureCreated()` (`SqliteConfig.cs:30`).
   - Seed creature templates from `resources/creature_templates.json` if empty (`SqliteConfig.cs:33-37`).
   - Seed part templates from `resources/part_templates.json` if empty (`SqliteConfig.cs:39-43`).
6. `BlazeServer` Redirector (`isSecure=true`) — `Program.cs:126`.
7. `BlazeServer` Lobby (`isSecure=false`) — `Program.cs:130`, attaches:
   - `AssociationListsComponent`
   - `AuthenticationComponent`
   - `GameManagerComponent` (with `GameHandler = sharedGameService`)
   - `MessagingComponent`
   - `PlaygroupsComponent`
   - `RoomsComponent`
   - `UserSessionsComponent`
   - `UtilComponent`
   - `GameReportingComponent`
   - `UnknownComponent1`
   - (registered in `BlazeServer.cs:61-72`)
8. `RakNetServer` :42000 (UDP) — `Program.cs:134`.
9. REST `Api` — `Program.cs:140`.

### Threading model

- Each adapter owns its own loop:
  - `BlazeServer.Start()` schedules `Task.Run(Run)` (`BlazeServer.cs:83`).
  - `RakNetServer.ExecuteAsync(token)` is awaited via `Task.Run` (`Program.cs:135`).
  - REST `Api.Run()` is called synchronously on the main thread (`Program.cs:143`).
- The main thread blocks inside `restClientAdapter.Run()`. Cancellation goes through the `CancellationTokenSource` plus the explicit `Stop()` chain in the elevation fallback (`Program.cs:151-157`).
- No equivalent of `boost::asio::io_context` or `Game::Scheduler`.

### Shutdown order

Implicit: process exit. The only explicit `Stop()` chain is in the `HttpListenerException` elevation-retry block (`Program.cs:151-157`), in the order `raknet.Listener.Stop() → lobby.Stop() → redirector.Stop() → restClientAdapter.Stop()`.

---

## Parity table

| Item | C++ | C# | Status |
|---|---|---|---|
| Entry point | `Main.cpp:303` | `Program.cs:27` | ✅ |
| CLI args parsed | `--timestamps`, `--version`, `--darkspore-path`, `--help` | `--port`, `--database-path`, `--assetdata-path`, `--help` | ⚠️ Different sets. C++ uses `darkspore-path`; C# uses `assetdata-path` for the package file directly. |
| Config source | `config.xml` + hardcoded defaults | Hardcoded `ServerConfigOptions` only | ⚠️ No XML loader on C# side. |
| Shared event loop | `boost::asio::io_context` (`Main.cpp:46`) | None — one `Task.Run` per adapter | ⚠️ No back-pressure across adapters. |
| Scheduler | `Game::Scheduler` (`Main.cpp:110`) | None | ❌ |
| Redirector server | `Main.cpp:132`, port 42127 | `Program.cs:126`, port 42127 | ✅ |
| Lobby (Blaze main) | `Main.cpp:133`, default port **10041** | `Program.cs:130`, port **42125** hardcoded | ⚠️ Port differs; redirector advertises the correct one regardless. |
| PSS server | `Main.cpp:135`, port 8443 | absent | ❌ |
| Tick server | `Main.cpp:136`, port 8999 | absent | ❌ |
| Telemetry server | `Main.cpp:137`, port 9988 | absent | ❌ |
| QoS server (UDP) | `Main.cpp:140`, port 3659 | absent | ❌ |
| HTTP main | `Main.cpp:143`, port 8033 | `Program.cs:140`, default 9000 / `--port=` | ⚠️ Port and host listener style differ (asio vs `HttpListener`). |
| HTTP telemetry | `Main.cpp:144`, port 8080 | absent | ❌ |
| HTTP QoS | `Main.cpp:145`, port 17502 | absent | ❌ |
| HTTP router shared between 3 servers | `Main.cpp:147-149` | n/a (only one HTTP server) | ❌ |
| RakNet server | constructed via `Game::Instance` / scheduler | `Program.cs:134`, port 42000 hardcoded | ⚠️ Different lifetime owner. |
| Persistent store | `SporeNet::Instance` (in-memory; XML + JSON files) | `SqliteConfig` (EF Core / SQLite) | ⚠️ Completely different storage. |
| Asset / noun warm-up | Background thread eagerly populates `NounDatabase`, Lua scripts, installer copies game data | Lazy: `AssetDatabase` constructed only if `--assetdata-path` provided; reads on demand | ⚠️ Eager vs lazy. Verify the client tolerates a cold cache on first lookup. |
| Signal handling | `mSignals` async (SIGINT/SIGTERM stops `io_service`) | None — `Ctrl+C` kills the process | ⚠️ No clean shutdown. |

---

## Open audit items

1. **Lobby port mismatch (10041 vs 42125).** Confirm whether the Redirector's `ServerInstanceInfo` reply on the C# side advertises 42125 correctly so this only matters at config level. If both ends pick the same advertised port, no functional issue.
2. **Missing PSS / Tick / Telemetry / QoS / HTTP-aux servers.** Does the client retry forever when those ports refuse, or does it skip silently? Capture a `tcpdump` from a working session to find out.
3. **Lazy `AssetDatabase`** versus C++ eager `NounDatabase`. First gameplay lookup on the C# side performs disk I/O; if that happens inside the 50 ms tick it could blow the budget.
4. **No `Scheduler` equivalent.** Anything in C++ that schedules a delayed task currently has no peer. Inventory the C++ callers of `mScheduler` and decide on a host (likely `Game.cs` `Task.Delay` helpers).
5. **No `config.xml`.** Decide whether to honor the same XML as the C++ build for parity, or to keep CLI-only and document the discrepancy.

---

## Files referenced

C++:

- `ReCap.Cpp/darkspore_server/source/Main.cpp`
- `ReCap.Cpp/darkspore_server/source/Main.h`
- `ReCap.Cpp/darkspore_server/source/Game/Config.h`
- `ReCap.Cpp/darkspore_server/source/Game/Config.cpp`

C#:

- `ReCap.Server/Program.cs`
- `ReCap.Server/Config/ServerConfig.cs`
- `ReCap.Server/Config/ServerConfigOptions.cs`
- `ReCap.Server/Config/SqliteConfig.cs`
- `ReCap.Server/Adapters/Blaze/BlazeServer.cs`
- `ReCap.Server/Adapters/RakNet/RakNetServer.cs`
