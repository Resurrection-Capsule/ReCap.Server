using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;

namespace ReCap.Server.Config;

public static class ServerConfig
{
    public static string HostName
    {
        get => _currentOpts.HostName;
    }
    public static IPAddress HostIP
    {
        get => _currentOpts.HostIP;
    }
    public static Version GameVersion
    {
        get => _currentOpts.GameVersion;
    }
    public static string GameVersionStr
    {
        get => GameVersion.ToString();
    }
    public static string ServerDatabasePath
    {
        get => Path.Join(ServerDatabaseDirectory, "server.db");
    }
    public static string ServerDatabaseDirectory
    {
        get => _currentOpts.ServerDatabaseDirectory;
    }
    public static string GamePath
    {
        get => _currentOpts.GamePath;
    }
    public static string GameRoot
    {
        get => _currentOpts.GameRoot;
    }
    public static string DataDir
    {
        get => _currentOpts.DataDir;
    }


    public static readonly string ResourcesDirectory = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "resources");




    static ServerConfigOptions _currentOpts = new();
    public static void Configure(ServerConfigOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        _currentOpts = options;
    }

    public static ServerConfigOptions CopyCurrentOptions()
        => new()
        {
            HostName = _currentOpts.HostName,
            HostIP = _currentOpts.HostIP,
            GameVersion = _currentOpts.GameVersion,
            ServerDatabaseDirectory = _currentOpts.ServerDatabaseDirectory,
            GamePath = _currentOpts.GamePath,
            GameRoot = _currentOpts.GameRoot,
            DataDir = _currentOpts.DataDir,
        };
}