using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;

#nullable disable
namespace ReCap.Server.Config;

public sealed class ServerConfigOptions
{
    public static readonly string DEFAULT_HOST_NAME = "localhost";
    string _hostName = DEFAULT_HOST_NAME;
    public string HostName
    {
        get => _hostName;
        set => _hostName = value;
    }


    public static readonly IPAddress DEFAULT_HOST_IP = IPAddress.Parse("127.0.0.1");
    IPAddress _hostIP = DEFAULT_HOST_IP;
    public IPAddress HostIP
    {
        get => _hostIP;
        set => _hostIP = value;
    }


    public static readonly Version DEFAULT_GAME_VERSION = new(5, 3, 0, 127);
    Version _gameVersion = DEFAULT_GAME_VERSION;
    public Version GameVersion
    {
        get => _gameVersion;
        set => _gameVersion = value;
    }


    public static readonly string DEFAULT_SERVER_DATABASE_DIRECTORY = AppDomain.CurrentDomain.BaseDirectory;
    string _serverDatabaseDirectory = DEFAULT_SERVER_DATABASE_DIRECTORY;
    public string ServerDatabaseDirectory
    {
        get => _serverDatabaseDirectory;
        set => _serverDatabaseDirectory = value;
    }

    public static readonly string DEFAULT_GAME_PATH = string.Empty;
    string _assetDataPath = DEFAULT_GAME_PATH;
    public string GamePath
    {
        get => _assetDataPath;
        set => _assetDataPath = value;
    }

    string _gameRoot = string.Empty;
    public string GameRoot
    {
        get => _gameRoot;
        set => _gameRoot = value;
    }

    string _dataDir = string.Empty;
    public string DataDir
    {
        get => _dataDir;
        set => _dataDir = value;
    }




    public ServerConfigOptions()
    {}
}
#nullable restore