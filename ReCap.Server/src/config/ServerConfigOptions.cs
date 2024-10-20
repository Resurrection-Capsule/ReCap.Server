using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;


#nullable disable
namespace ReCap.Server.Config.Server
{
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




        public ServerConfigOptions()
        {}
    }
    public static class ServerConfigOptionsExtensions
    {
        public static ServerConfigOptions Clone(this ServerConfigOptions self)
            => new()
            {
                HostName = self.HostName,
                HostIP = self.HostIP,
                GameVersion = self.GameVersion,
                ServerDatabaseDirectory = self.ServerDatabaseDirectory,
            };




        public static ServerConfigOptions WithHostName(this ServerConfigOptions self, string hostName)
        {
            ServerConfigOptions ret = self.Clone();
            if (!string.IsNullOrWhiteSpace(hostName))
                ret.HostName = hostName;
            return ret;
        }


        public static ServerConfigOptions WithHostIP(this ServerConfigOptions self, IPAddress hostIP)
        {
            ServerConfigOptions ret = self.Clone();
            if (hostIP != null)
                ret.HostIP = hostIP;
            return ret;
        }




        public static ServerConfigOptions WithGameVersion(this ServerConfigOptions self, Version gameVersion)
        {
            ServerConfigOptions ret = self.Clone();
            if (gameVersion != null)
                ret.GameVersion = gameVersion;
            return ret;
        }


        public static ServerConfigOptions WithDatabaseDirectory(this ServerConfigOptions self, string serverDatabaseDirectory)
        {
            ServerConfigOptions ret = self.Clone();
            if (!string.IsNullOrWhiteSpace(serverDatabaseDirectory))
                ret.ServerDatabaseDirectory = serverDatabaseDirectory;
            return ret;
        }
    }
}
#nullable restore