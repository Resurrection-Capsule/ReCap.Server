namespace ReCap.Server.Adapters.Persistence.StaticStorage;
    
using System;
using System.IO;
using System.Net;
using System.Text;

using ReCap.Server.Config.Server;

public class StaticStorageAdapter
{
    static string STATIC_RESOURCES_FOLDER_PATH = "./resources/static";

    public static byte[] GetFile(string filePath)
    {
        string fullPath = STATIC_RESOURCES_FOLDER_PATH + filePath;
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(fullPath);
        }
        if (filePath.EndsWith(".html") || filePath.EndsWith(".js")) {
            string host = ServerConfig.GetHost();
            string fileContents = File.ReadAllText(fullPath);
            fileContents = fileContents.Replace("{{host}}", host);
            fileContents = fileContents.Replace("{{recap-version}}", "1.0");
            fileContents = fileContents.Replace("{{game-mode}}", "singleplayer");
            fileContents = fileContents.Replace("{{isDev}}", "true");
            return Encoding.UTF8.GetBytes(fileContents);
        }
        return File.ReadAllBytes(fullPath);
    }
}
