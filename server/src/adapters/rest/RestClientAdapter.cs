using System;
using System.IO;
using System.Net;
using System.Text;

using HttpServer;

namespace HttpServer
{
    class RestClientAdapter
    {
        public static void Run()
        {
            string host = ServerConfig.GetHost();
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(String.Format("http://{0}/", host));
            listener.Start();

            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                ProcessRequest(context);
            }
        }

        static void ProcessRequest(HttpListenerContext context)
        {
            byte[] fileBytes = null;

            try
            {
                string uri = context.Request.Url.LocalPath;

                if (uri == "/bootstrap/launcher/")
                {
                    fileBytes = StaticStorageAdapter.GetFile("/bootstrap/launcher/index.html");
                }
                else if (uri == "/bootstrap/launcher/notes")
                {
                    fileBytes = StaticStorageAdapter.GetFile("/bootstrap/launcher/notes.html");
                }
                else if (uri.StartsWith("/recap/api"))
                {
                    fileBytes = ReCapService.HandleRequest(context);
                }
                else if (uri.StartsWith("/bootstrap/api"))
                {
                    fileBytes = LauncherService.HandleRequest(context);
                }
                else if (uri.StartsWith("/game/api"))
                {
                    fileBytes = GameService.HandleRequest(context);
                }
                else
                {
                    fileBytes = StaticStorageAdapter.GetFile(uri);
                }
            }
            catch (FileNotFoundException ex)
            {
                context.Response.StatusCode = 404;
                context.Response.StatusDescription = "File not found: " + ex.Message;
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                context.Response.StatusDescription = "Error serving file: " + ex.Message;
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                if (fileBytes != null) {
                    context.Response.ContentLength64 = fileBytes.Length;
                    context.Response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
                }
                context.Response.Close();
            }
        }
    }
}
