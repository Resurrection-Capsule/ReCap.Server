using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ReCap.Server.Adapters.Blaze;
using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.Rest.Api;
using ReCap.Server.Config;
using ReCap.Server.Services;
using ReCap.Server.Util;

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
    const string _GAME_PATH_ARG = "--game-path=";
    static async Task Main(string[] args)
    {
#nullable disable
        string databasePath = null;
        string gamePath = null;
        int port = Api.DEFAULT_PORT;


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
                string gPath = arg.Substring(_GAME_PATH_ARG.Length);
                gPath = CommandLineHelper.UnwrapArg(gPath);

                if (File.Exists(gPath))
                    gamePath = gPath;
                else
                    Logger.error($"Game path not found: '{gPath}'");
            }
        }


        if (ProcessPermissions.IsCurrentProcessElevated)
        {
            Logger.info($"Running {GetRelaunchAsWhat()}!");
        }
        else if (port == Api.DEFAULT_PORT)
        {
            Logger.info($"Must relaunch {GetRelaunchAsWhat()} to proceed...");
            if (await TryRelaunchElevatedAsync())
                return;
        }


        string portDbgLine = "Running on ";
        if (port == Api.DEFAULT_PORT)
            portDbgLine += "default ";
        Logger.info(portDbgLine + $"port {port}");
        
        var serverOpts = ServerConfig.CopyCurrentOptions();
#nullable restore
        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            Logger.info($"Using DB path: '{databasePath}'");
            serverOpts.ServerDatabaseDirectory = databasePath;
        }

        if (!string.IsNullOrWhiteSpace(gamePath))
        {
            Logger.info($"Using game path: '{gamePath}'");
            serverOpts.GamePath = gamePath;
        }

        ServerConfig.Configure(serverOpts);

        var assetDatabase = string.IsNullOrWhiteSpace(ServerConfig.GamePath)
            ? null
            : new AssetDatabase(ServerConfig.GamePath);

        var gameService = new GameService { Assets = assetDatabase };

        CancellationTokenSource source = new CancellationTokenSource();
        CancellationToken token = source.Token;

        var dbConfig = new SqliteConfig();
        dbConfig.Start();


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
        $"{_BEFORE_ARG}{_GAME_PATH_ARG}<str>{_AFTER_ARG}Path to the Darkspore AssetData_Binary.package file",
    }.AsReadOnly();
    static void PrintHelp()
    {
        foreach (string line in _HELP)
        {
            Logger.info(line);
        }
    }
}
