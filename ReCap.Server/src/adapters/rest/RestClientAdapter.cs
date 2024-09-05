using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using HttpServer;
using HttpMultipartParser;

namespace HttpServer;

public class RestClientAdapter
{
    private BootstrapRestClientAdapter bootstrapRestClientAdapter;
    private GameRestClientAdapter gameRestClientAdapter;
    private ReCapRestClientAdapter reCapRestClientAdapter;
    private SurveyRestClientAdapter surveyRestClientAdapter;

    public RestClientAdapter(SqliteConfig newSqliteConfig) {
        bootstrapRestClientAdapter = new BootstrapRestClientAdapter();
        gameRestClientAdapter = new GameRestClientAdapter(newSqliteConfig);
        reCapRestClientAdapter = new ReCapRestClientAdapter(newSqliteConfig);
        surveyRestClientAdapter = new SurveyRestClientAdapter();
    }

    public void Run()
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

    private Dictionary<string,string> GetParameters(HttpListenerContext context)
    {
        var parameters = new Dictionary<string,string>();

        // Query parameters
        var query = context.Request.QueryString;
        foreach (string key in query.Keys) {
            parameters.Add(key, query.Get(key));
        }

        // Multipart form parameters
        if (context.Request.HttpMethod == "POST") {
            try {
                var inputStream = context.Request.InputStream;
                var parser = MultipartFormDataParser.Parse(inputStream);
                foreach(var entry in parser.Parameters) {
                    parameters.Add(entry.Name, entry.Data);
                }
            }
            catch (Exception ex) {}
        }

        // Cookies parameters
        var cookies = context.Request.Cookies;
        foreach(Cookie cookie in cookies) {
            if (parameters.ContainsKey(cookie.Name)) {
                if (parameters[cookie.Name] == "cookie") {
                    parameters[cookie.Name] = cookie.Value;
                }
            }
        }

        if (parameters.Count > 0) {
            Console.WriteLine($"Parameters: {string.Join(", ", parameters)}");
        }

        return parameters;
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        var parameters = GetParameters(context);
        string uri = context.Request.Url.LocalPath;
        byte[] fileBytes = null;

        try
        {
            if (uri == "/bootstrap/launcher/")
            {
                fileBytes = StaticStorageAdapter.GetFile("/bootstrap/launcher/wrapper.html");
            }
            else if (uri == "/bootstrap/launcher/notes")
            {
                fileBytes = new byte[]{};
            }
            else if (uri.StartsWith("/recap/api"))
            {
                var method = GetMethod(typeof(ReCapRestClientAdapter), parameters["method"]);
                fileBytes = (byte[])method.Invoke(reCapRestClientAdapter, new object[] { context });
            }
            else if (uri.StartsWith("/bootstrap/api"))
            {
                var method = GetMethod(typeof(BootstrapRestClientAdapter), parameters["method"]);
                fileBytes = (byte[])method.Invoke(bootstrapRestClientAdapter, new object[] { context });
                context.Response.ContentType = "text/xml";
            }
            else if (uri.StartsWith("/game/api"))
            {
                var method = GetMethod(typeof(GameRestClientAdapter), parameters["method"]);
                fileBytes = (byte[])method.Invoke(gameRestClientAdapter, new object[] { context, parameters });
                context.Response.ContentType = "text/xml";
            }
            else if (uri.StartsWith("/survey/api"))
            {
                var method = GetMethod(typeof(SurveyRestClientAdapter), parameters["method"]);
                fileBytes = (byte[])method.Invoke(surveyRestClientAdapter, new object[] { context });
                context.Response.ContentType = "text/xml";
            }
            else if (uri == "/web/sporelabsgame/register")
            {
                fileBytes = StaticStorageAdapter.GetFile("/bootstrap/register/index.html");
            }
            else if (uri.StartsWith("/web/sporelabsgame/register/"))
            {
                fileBytes = StaticStorageAdapter.GetFile(uri.Replace("/web/sporelabsgame/", "/bootstrap/"));
            }
            else
            {
                fileBytes = StaticStorageAdapter.GetFile(uri);
            }

            if (fileBytes != null) {
                context.Response.ContentLength64 = fileBytes.Length;
                context.Response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
                Console.WriteLine($"[RestClientAdapter] {context.Request.RawUrl} Success 200");
            }
            else {
                context.Response.StatusCode = 501;
                context.Response.StatusDescription = "Method not implemented";
                Console.WriteLine($"[RestClientAdapter] {context.Request.RawUrl} Error 501: {parameters.GetValueOrDefault("method", "<unknown>")}");
            }
            context.Response.Close();
        }
        catch (BadRequestException ex)
        {
            context.Response.StatusCode = 400;
            context.Response.StatusDescription = ex.Message;
            context.Response.Close();
            Console.WriteLine($"[RestClientAdapter] {context.Request.RawUrl} Error 400: {context.Response.StatusDescription}");
        }
        catch (ForbiddenOperationException ex)
        {
            context.Response.StatusCode = 403;
            context.Response.StatusDescription = ex.Message;
            context.Response.Close();
            Console.WriteLine($"[RestClientAdapter] {context.Request.RawUrl} Error 403: {context.Response.StatusDescription}");
        }
        catch (FileNotFoundException ex)
        {
            context.Response.StatusCode = 404;
            context.Response.StatusDescription = "File not found: " + ex.Message;
            context.Response.Close();
            Console.WriteLine($"[RestClientAdapter] {context.Request.RawUrl} Error 404: {context.Response.StatusDescription}");
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            context.Response.StatusDescription = "Error serving file: " + ex.Message;
            Console.WriteLine($"[RestClientAdapter] {context.Request.RawUrl} Error 500: {ex.ToString()}");
            context.Response.Close();
        }
    }

    private MethodInfo GetMethod(Type serviceType, string methodName)
    {
        if (methodName == null) {
            throw new BadRequestException("Method must not be null");
        }

        MethodInfo[] methods = serviceType.GetMethods(BindingFlags.Instance | BindingFlags.Public);

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
