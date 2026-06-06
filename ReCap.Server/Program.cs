using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ReCap.Server.Adapters.Blaze;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.Rest.Api;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Adapters.Scripting.Api;
using ReCap.Server.Config;
using ReCap.Server.Services;
using ReCap.Server.Services.Scripting;
using ReCap.Server.Util;
using ReCap.Server.Util.Logging;
using Serilog.Events;

namespace ReCap.Server;

public static class Program
{
    static readonly IReadOnlyList<int> _HTTP_RELAUNCH_ERROR_CODES = new List<int>()
    {
        5,
        13
    }.AsReadOnly();
    const string _HELP_ARG = "--help";
    const string _PORT_ARG = "--port=";
    const string _DB_PATH_ARG = "--database-path=";
    const string _ASSETDATA_PATH_ARG = "--assetdata-path=";
    const string _GAME_PATH_ARG = "--game-path=";
    const string _RAKNET_VERBOSE_ARG = "--raknet-verbose";
    const string _VERBOSE_ARG = "--verbose";
    const string _LOG_LEVEL_ARG = "--log-level=";
    const string _TELEPORT_MOVEMENT_ARG = "--teleport-movement";
    const string _LUA_SMOKE_ARG = "--lua-smoke";
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var (logLevel, logOverrides) = ParseLogConfig(args);
        LoggingConfig.Bootstrap(logLevel, logOverrides);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => LoggingConfig.CloseAndFlush();
        PrintBanner();
#nullable disable
        string databasePath = null;
        string cliGamePath = null;
        string cliAssetDataPath = null;
        int port = Api.DEFAULT_PORT;
        bool luaSmoke = false;


        int argCount = args.Length;
        for (int i = 0; i < argCount; i++)
        {
            string arg = CommandLineHelper.UnwrapArg(args[i]);
            if (arg == _HELP_ARG)
            {
                PrintHelp();
                return;
            }
            else if ((port == Api.DEFAULT_PORT) && arg.StartsWith(_PORT_ARG))
            {
                string portStr = arg.Substring(_PORT_ARG.Length);
                if (int.TryParse(portStr, out int p))
                    port = p;
            }
            else if (arg.StartsWith(_DB_PATH_ARG))
            {
                string dbPath = arg.Substring(_DB_PATH_ARG.Length);
                dbPath = CommandLineHelper.UnwrapArg(dbPath);

                if (!Directory.Exists(dbPath))
                    Directory.CreateDirectory(dbPath);

                if (Directory.Exists(dbPath))
                {
                    databasePath = dbPath;
                }
            }
            else if (arg.StartsWith(_GAME_PATH_ARG))
            {
                cliGamePath = CommandLineHelper.UnwrapArg(arg.Substring(_GAME_PATH_ARG.Length));
            }
            else if (arg.StartsWith(_ASSETDATA_PATH_ARG))
            {
                Log.Server.Warn("--assetdata-path is deprecated, use --game-path");
                cliAssetDataPath = CommandLineHelper.UnwrapArg(arg.Substring(_ASSETDATA_PATH_ARG.Length));
            }
            else if (arg == _TELEPORT_MOVEMENT_ARG)
            {
                Domain.Gameplay.Game.TeleportMovement = true;
                Log.Server.Info("Movement mode: teleport (snap-per-click fallback enabled)");
            }
            else if (arg == _LUA_SMOKE_ARG)
            {
                luaSmoke = true;
            }
        }


        if (ProcessPermissions.IsCurrentProcessElevated)
        {
            Log.Server.Info($"Running {GetRelaunchAsWhat()}!");
        }
        else if (port == Api.DEFAULT_PORT)
        {
            Log.Server.Warn($"Must relaunch {GetRelaunchAsWhat()} to proceed...");
            if (await TryRelaunchElevatedAsync())
                return;
        }


        var serverOpts = ServerConfig.CopyCurrentOptions();
#nullable restore
        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            Log.Server.Info($"Using DB path: '{databasePath}'");
            serverOpts.ServerDatabaseDirectory = databasePath;
        }

        var persistencePath = Path.Combine(serverOpts.ServerDatabaseDirectory, "game-path.json");
        var install = Services.GameInstallLocator.Resolve(cliGamePath ?? cliAssetDataPath, persistencePath);
        if (install is not null)
        {
            Log.Server.Info($"Game install: '{install.Root}'");
            serverOpts.GameRoot = install.Root;
            serverOpts.DataDir = install.DataDir;
            serverOpts.GamePath = Path.Combine(install.DataDir, "AssetData_Binary.package");
        }
        else
        {
            Log.Server.Warn("Game install not found (tried: CLI arg, persisted game-path.json, registry, default probe paths). Use --game-path=<game folder> to set it.");
        }

        ServerConfig.Configure(serverOpts);

        AssetDatabase? assetDatabase = null;
        if (!string.IsNullOrWhiteSpace(ServerConfig.GamePath))
        {
            assetDatabase = new AssetDatabase(ServerConfig.GamePath);
            _ = assetDatabase.WarmUpAsync();
        }

        if (luaSmoke)
        {
            var mounts = PackageMounts.Default;
            if (mounts is not null)
            {
                var vfs = new ScriptVfs(mounts);
                var engine = new ScriptEngine(vfs);
                BootReport smokeReport;
                using (var smokeRt = LuaRuntime.CreateSandboxedState(n => vfs.GetChunk(ScriptVfs.ParseReference(n))))
                    smokeReport = engine.ExecuteBootScripts(smokeRt);
                Log.Lua.Info($"[lua-smoke] total={smokeReport.Total} failures={smokeReport.Failures.Count} retail-missing={smokeReport.MissingRequires.Count}");
                foreach (var f in smokeReport.Failures.Take(10))
                    Log.Lua.Info($"[lua-smoke] FAIL: {f}");
                foreach (var m in smokeReport.MissingRequires.Take(20))
                    Log.Lua.Warn($"[lua-smoke] MISSING: {m}");
                var snap = StubTelemetry.Snapshot();
                Log.Lua.Info($"[lua-smoke] stub-telemetry count={snap.Count}");
                foreach (var entry in snap)
                    Log.Lua.Debug($"[lua-smoke] stub: {entry}");
            }
            else
            {
                Log.Lua.Warn("[lua-smoke] no game data path — skipped");
            }
        }

        CancellationTokenSource source = new CancellationTokenSource();
        CancellationToken token = source.Token;

        var dbConfig = new SqliteConfig();
        dbConfig.Start();

        var gameService = new GameService
        {
            Assets = assetDatabase,
            Decks = new DeckService(dbConfig),
            Creatures = new CreatureService(dbConfig)
        };


#pragma warning disable CS4014
        IPAddress localhostIP = ServerConfig.HostIP;
        string hostname = ServerConfig.HostName;

        BlazeServer redirector = new(dbConfig, "Redirector", localhostIP, 42127, true, hostname);
        Task.Run(redirector.Start);


        BlazeServer lobby = new(dbConfig, "Lobby", localhostIP, 42125, false, hostname, gameService);
        Task.Run(lobby.Start);


        RakNetServer raknet = new(dbConfig, "RakNet", localhostIP, 42000, false, hostname, assetDatabase, gameService);
        Task.Run(() => raknet.ExecuteAsync(token));
#pragma warning restore CS4014

        Log.Server.Info(
            "Listening:\n" +
            $"    Redirector  {localhostIP}:42127  TLS\n" +
            $"    Lobby       {localhostIP}:42125\n" +
            $"    RakNet      {localhostIP}:42000  UDP\n" +
            $"    REST        {localhostIP}:{port}");

        Api restClientAdapter = new(dbConfig, port);
        try
        {
            restClientAdapter.Run();
        }
        catch (HttpListenerException ex)
        {
            var errorCode = ex.ErrorCode;
            if (!_HTTP_RELAUNCH_ERROR_CODES.Contains(errorCode))
                throw;

            Task afterRelaunch = new(() =>
            {
                raknet.Listener.Stop();
                lobby.Stop();
                redirector.Stop();
                restClientAdapter.Stop();
            });

            if (!await TryRelaunchElevatedAsync(false, afterRelaunch))
                throw;
        }
    }


#nullable disable
    static string GetRelaunchAsWhat()
        => PlatformInfo.IsWindows
            ? "with administrator privileges"
            : "as root"
        ;


    static async Task<bool> TryRelaunchElevatedAsync(bool returnIfAlreadyElevated = true, Task onLaunchSucceeded = default)
    {
        if (ProcessPermissions.IsCurrentProcessElevated)
            return returnIfAlreadyElevated;

        var process = await ProcessPermissions.RerunElevatedAsync();
        if (process == null)
            return false;

        if (onLaunchSucceeded != null)
            await onLaunchSucceeded;

        await process.WaitForExitAsync();
        var exitCode = process.ExitCode;
        Environment.Exit(exitCode);
        return true;
    }
#nullable restore
    const string _BEFORE_ARG = "    ";
    const string _AFTER_ARG = "        ";
    static readonly IReadOnlyList<string> _HELP = new List<string>()
    {
        $"ReCap server CLI help (UNDER CONSTRUCTION)",
        string.Empty,
        $"{_BEFORE_ARG}{_PORT_ARG}<int>         {_AFTER_ARG}Port number",
        $"{_BEFORE_ARG}{_DB_PATH_ARG}<str>{_AFTER_ARG}Path to a directory in which to create/store/access the 'server.db'",
        $"{_BEFORE_ARG}{_GAME_PATH_ARG}<str>    {_AFTER_ARG}Path to the Darkspore install folder (or Data/ subfolder, or AssetData_Binary.package)",
        $"{_BEFORE_ARG}{_ASSETDATA_PATH_ARG}<str>{_AFTER_ARG}[deprecated] Use --game-path instead",
        $"{_BEFORE_ARG}{_LOG_LEVEL_ARG}<lvl>     {_AFTER_ARG}Global log level, or <Category>:<lvl> (e.g. RakNet:verbose)",
        $"{_BEFORE_ARG}{_VERBOSE_ARG}            {_AFTER_ARG}Shortcut for --log-level=debug",
        $"{_BEFORE_ARG}{_RAKNET_VERBOSE_ARG}    {_AFTER_ARG}Shortcut for --log-level=RakNet:verbose",
    }.AsReadOnly();
    static void PrintHelp()
    {
        foreach (string line in _HELP)
            Console.WriteLine(line);
    }

    static (LogEventLevel, Dictionary<string, LogEventLevel>) ParseLogConfig(string[] args)
    {
        var global = LogEventLevel.Information;
        var overrides = new Dictionary<string, LogEventLevel>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in args)
        {
            var arg = CommandLineHelper.UnwrapArg(raw);
            if (arg == _RAKNET_VERBOSE_ARG)
            {
                overrides[LogCategories.RakNet] = LogEventLevel.Verbose;
            }
            else if (arg == _VERBOSE_ARG)
            {
                global = LogEventLevel.Debug;
            }
            else if (arg.StartsWith(_LOG_LEVEL_ARG))
            {
                var value = arg.Substring(_LOG_LEVEL_ARG.Length);
                var sep = value.IndexOf(':');
                if (sep < 0)
                {
                    if (LoggingConfig.TryParseLevel(value, out var lvl))
                        global = lvl;
                }
                else if (LoggingConfig.TryParseLevel(value[(sep + 1)..], out var lvl))
                {
                    var category = value[..sep];
                    overrides[NormalizeCategory(category)] = lvl;
                }
            }
        }

        return (global, overrides);
    }

    static string NormalizeCategory(string category)
        => Array.Find(LogCategories.All, c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase)) ?? category;

    static void PrintBanner()
    {
        var v = ServerConfigOptions.DEFAULT_GAME_VERSION;
        Console.WriteLine();
        Console.WriteLine(@"   ____      ____            ");
        Console.WriteLine(@"  |  _ \ ___/ ___|__ _ _ __  ");
        Console.WriteLine(@"  | |_) / _ \ |  / _` | '_ \ ");
        Console.WriteLine(@"  |  _ <  __/ |_| (_| | |_) |");
        Console.WriteLine(@"  |_| \_\___|\____\__,_| .__/ ");
        Console.WriteLine(@"                       |_|    ");
        Console.WriteLine($"  Darkspore private server · v{v}");
        Console.WriteLine();
    }
}
