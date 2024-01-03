using System;
using System.IO;
using System.Net;
using System.Text;

namespace HttpServer
{
    class StaticStorageAdapter
    {
        static string STATIC_RESOURCES_FOLDER_PATH = "./resources/static";

        public static byte[] GetHtmlFile(string filePath, string host)
        {
            string fullPath = STATIC_RESOURCES_FOLDER_PATH + filePath;
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(fullPath);
            }
            string fileContents = File.ReadAllText(fullPath);
            fileContents = fileContents.Replace("{{host}}", host);
            fileContents = fileContents.Replace("{{recap-version}}", "1.0");
            fileContents = fileContents.Replace("{{game-mode}}", "singleplayer");
            return Encoding.UTF8.GetBytes(fileContents);
        }

        public static byte[] GetFile(string filePath)
        {
            string fullPath = STATIC_RESOURCES_FOLDER_PATH + filePath;
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(fullPath);
            }
            return File.ReadAllBytes(fullPath);
        }
    }
}
