using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using HttpServer;
using HttpMultipartParser;
using LoggerUtil;

namespace HttpServer;

public class Api
{
    private List<object> restControllers;

    public Api(SqliteConfig newSqliteConfig) {
        restControllers = new List<object>();
        var assembly = Assembly.GetExecutingAssembly();
        foreach(Type type in assembly.GetTypes()) {
            if (type.GetCustomAttributes(typeof(RestController), true).Length > 0) {
                var instance = Activator.CreateInstance(type, [newSqliteConfig]);
                restControllers.Add(instance);
            }
        }
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
            Logger.debug($"Parameters: {string.Join(", ", parameters)}");
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
            foreach(object restController in restControllers)
            {
                var restControllerType = restController.GetType();
                if (isRestController(restControllerType, uri))
                {
                    var method = GetMethod(restControllerType, parameters["method"]);
                    fileBytes = (byte[])method.Invoke(restController, new object[] { context, parameters });
                    context.Response.ContentType = GetContentType(restControllerType);
                }
            }
            
            if (fileBytes == null) {
                fileBytes = GetBytesByFilePath(uri);
            }

            if (fileBytes == null) {
                throw new UnimplementedMethodException(parameters.GetValueOrDefault("method", "<unknown>"));
            }

            context.Response.ContentLength64 = fileBytes.Length;
            context.Response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
            Logger.debug($"[RestClientAdapter] {context.Request.RawUrl} Success 200");
            context.Response.Close();
        }
        catch (Exception ex)
        {
            var method = GetExceptionMethod(ex.GetType());
            method.Invoke(typeof(GlobalExceptionHandler), new object[] { context, ex });
        }
    }

    private byte[] GetBytesByFilePath(string uri)
    {
        if (uri == "/bootstrap/launcher/")
        {
            return StaticStorageAdapter.GetFile("/bootstrap/launcher/wrapper.html");
        }
        if (uri == "/bootstrap/launcher/notes")
        {
            return new byte[]{};
        }
        if (uri.StartsWith("/web/sporelabsgame/"))
        {
            if (Regex.IsMatch(uri, @"^/web/sporelabsgame/[a-zA-Z]+$"))
            {
                return StaticStorageAdapter.GetFile(uri.Replace("/web/sporelabsgame/", "/bootstrap/") + "/index.html");
            }
            return StaticStorageAdapter.GetFile(uri.Replace("/web/sporelabsgame/", "/bootstrap/"));
        }
        return StaticStorageAdapter.GetFile(uri);
    }

    private MethodInfo GetMethod(Type serviceType, string methodName)
    {
        if (methodName == null) {
            throw new BadRequestException("Method must not be null");
        }

        MethodInfo[] methods = serviceType.GetMethods(BindingFlags.Instance | BindingFlags.Public);

        foreach (MethodInfo method in methods)
        {
            if (method.GetCustomAttribute(typeof(RequestMapping)) != null &&
                ((RequestMapping)method.GetCustomAttribute(typeof(RequestMapping))).Name == methodName)
            {
                return method;
            }
        }

        throw new Exception("Invalid method " + methodName);
    }

    private MethodInfo GetExceptionMethod(Type exceptionType)
    {
        MethodInfo[] methods = typeof(GlobalExceptionHandler).GetMethods(BindingFlags.Static | BindingFlags.Public);

        foreach (MethodInfo method in methods)
        {
            if (method.GetCustomAttribute(typeof(ExceptionHandler)) != null &&
                ((ExceptionHandler)method.GetCustomAttribute(typeof(ExceptionHandler))).Type == exceptionType)
            {
                return method;
            }
        }

        return GetExceptionMethod(exceptionType.BaseType);
    }

    private bool isRestController(System.Type restControllerType, string apiPath)
    {
        var dnAttribute = restControllerType.GetCustomAttributes(typeof(RestController), true).FirstOrDefault() as RestController;
        if (dnAttribute != null)
        {
            // return apiPath == dnAttribute.Value;
            return apiPath.StartsWith(dnAttribute.Value);
        }
        return false;
    }

    private string GetContentType(System.Type restControllerType)
    {
        var dnAttribute = restControllerType.GetCustomAttributes(typeof(RestController), true).FirstOrDefault() as RestController;
        if (dnAttribute != null)
        {
            return dnAttribute.ContentType;
        }
        return null;
    }
}
