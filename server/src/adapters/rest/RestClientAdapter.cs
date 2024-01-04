using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using HttpServer;

namespace HttpServer
{
    class RestClientAdapter
    {
        public static void Run()
        {
            string[] hosts = ServerConfig.GetDarksporeHosts();
            HttpListener listener = new HttpListener();
            for(int i = 0; i < hosts.Length; i++) {
                listener.Prefixes.Add(String.Format("http://{0}/", hosts[i]));
            }
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
                var query = context.Request.QueryString;
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
                    var method = GetMethod(typeof(ReCapService), query.Get("method"));
                    fileBytes = (byte[])method.Invoke(null, new object[] { query });
                }
                else if (uri.StartsWith("/bootstrap/api"))
                {
                    var method = GetMethod(typeof(LauncherService), query.Get("method"));
                    fileBytes = (byte[])method.Invoke(null, new object[] { query });
                    context.Response.ContentType = "text/xml";
                }
                else if (uri.StartsWith("/game/api"))
                {
                    var method = GetMethod(typeof(GameService), query.Get("method"));
                    fileBytes = (byte[])method.Invoke(null, new object[] { query });
                    context.Response.ContentType = "text/xml";
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

        private static MethodInfo GetMethod(Type serviceType, string methodName)
        {
            MethodInfo[] methods = serviceType.GetMethods(BindingFlags.Static | BindingFlags.Public);

            foreach (MethodInfo method in methods)
            {
                if (method.GetCustomAttribute(typeof(ApiMethod)) != null &&
                    ((ApiMethod)method.GetCustomAttribute(typeof(ApiMethod))).Name == methodName)
                {
                    return method;
                }
            }

            throw new Exception("Invalid method " + methodName);
        }
    }
}
