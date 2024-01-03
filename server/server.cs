using System;
using System.IO;
using System.Net;
using System.Text;

namespace BootstrapWebServer
{
    class Server
    {
        static void Main(string[] args)
        {
            string host = "localhost:8080";
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(String.Format("http://{0}/", host));
            listener.Start();

            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                ProcessRequest(context, host);
            }
        }

        static void ProcessRequest(HttpListenerContext context, string host)
        {
            string uri = context.Request.Url.LocalPath;

            if (uri == "/bootstrap/launcher/")
            {
                ServeHtmlStaticFile(context, "./resources/static/bootstrap/launcher/index.html", host);
            }
            else if (uri == "/bootstrap/launcher/notes")
            {
                ServeHtmlStaticFile(context, "./resources/static/bootstrap/launcher/notes.html", host);
            }
            else if (uri.StartsWith("/assets"))
            {
                ServeDirectoryFiles(context, "./resources/static");
            }
            else if (uri.StartsWith("/bootstrap/launcher/"))
            {
                ServeDirectoryFiles(context, "./resources/static");
            }
            else if (uri.StartsWith("/bootstrap/api"))
            {
                HandleBootstrapApiRequest(context);
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        }

        static void ServeHtmlStaticFile(HttpListenerContext context, string filePath, string host)
        {
            try
            {
                string fileContents = File.ReadAllText(filePath);
                fileContents = fileContents.Replace("{{host}}", host);
                fileContents = fileContents.Replace("{{recap-version}}", "1.0");
                fileContents = fileContents.Replace("{{game-mode}}", "singleplayer");
                byte[] fileBytes = Encoding.UTF8.GetBytes(fileContents);
        
                context.Response.ContentLength64 = fileBytes.Length;
                context.Response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                context.Response.StatusDescription = "Error serving file: " + ex.Message;
            }
            finally
            {
                context.Response.Close();
            }
        }

        static void ServeStaticFile(HttpListenerContext context, string filePath)
        {
            try
            {
                byte[] fileBytes = File.ReadAllBytes(filePath);
                context.Response.ContentLength64 = fileBytes.Length;
                context.Response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                context.Response.StatusDescription = "Error serving file: " + ex.Message;
            }
            finally
            {
                context.Response.Close();
            }
        }

        static void ServeDirectoryFiles(HttpListenerContext context, string directoryPath)
        {
            string requestedFile = context.Request.Url.LocalPath;

            if (!string.IsNullOrEmpty(requestedFile) && requestedFile != "/")
            {
                string filePath = directoryPath + requestedFile;
                if (File.Exists(filePath))
                {
                    ServeStaticFile(context, filePath);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
        }

        static void HandleBootstrapApiRequest(HttpListenerContext context)
        {
            // Parse GET parameters and handle API logic here
        }
    }
}
