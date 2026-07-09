using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Config;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.Rest.Api;

public class Api
{
    public const int DEFAULT_PORT = 8033;
    private List<object> restControllers;
    private readonly PngStorageAdapter _pngAdapter;
    private readonly QosStorageAdapter _qosAdapter;

    readonly int _port;
    public Api(SqliteConfig newSqliteConfig, int port = DEFAULT_PORT) {
        _port = port;
        _pngAdapter = new PngStorageAdapter(newSqliteConfig);
        _qosAdapter = new QosStorageAdapter(port);
        restControllers = new List<object>();
        var assembly = Assembly.GetExecutingAssembly();
        foreach(Type type in assembly.GetTypes()) {
            if (type.GetCustomAttributes(typeof(RestController), true).Length > 0) {
                var instance = Activator.CreateInstance(type, [newSqliteConfig]);
                if (instance is not null)
                    restControllers.Add(instance);
            }
        }
    }
#nullable disable
    HttpListener _listener = null;
    bool _run = false;
    public void Run()
    {
        if (_listener != null)
            return;

        _run = true;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{_port}/");
        _listener.Start();

        while (_run)
        {
            HttpListenerContext context = _listener.GetContext();
            ProcessRequest(context);
        }
    }

    public void Stop()
    {
        if (_listener == null)
            return;
        _listener.Stop();
        _run = false;
        _listener = null;
    }
#nullable restore


    private void ProcessRequest(HttpListenerContext context)
    {
        var parameters = HTTPHelper.GetParametersFromRequest(context.Request);
        if (parameters.Count > 0) {
            ReCap.Server.Util.Logging.Log.Rest.Debug($"Parameters: {string.Join(", ", parameters)}");
        }

        string uri = context.Request.Url?.LocalPath.Split("?")[0] ?? "/";
        byte[]? fileBytes = null;

        try
        {
            foreach(object restController in restControllers)
            {
                var restControllerType = restController.GetType();
                if (isRestController(restControllerType, uri))
                {
                    var methodName = parameters.GetValueOrDefault("method", "<unknown>");
                    var method = GetMethod(restControllerType, methodName);
                    fileBytes = (byte[]?)method.Invoke(restController, new object[] { context, parameters });
                    context.Response.ContentType = GetContentType(restControllerType, method);

                    if (fileBytes == null)
                    {
                        throw new UnimplementedMethodException(methodName);
                    }
                }
            }
            
            // Portrait PNGs (/template_png, /creature_png, /game/service/png) — checked before
            // GameStorageAdapter since /game/service/png also matches its /game/ prefix.
            if (fileBytes == null && _pngAdapter.Handles(uri))
            {
                fileBytes = _pngAdapter.GetFile(uri, parameters);
                context.Response.ContentType = "image/png";
            }

            if (fileBytes == null && _qosAdapter.Handles(uri))
            {
                fileBytes = _qosAdapter.GetFile(uri, parameters);
                context.Response.ContentType = "text/xml";
            }

            if (fileBytes == null && GameStorageAdapter.Handles(uri))
            {
                fileBytes = GameStorageAdapter.GetFile(uri);
                context.Response.ContentType = GameStorageAdapter.ContentType(uri);
            }

            if (fileBytes == null)
            {
                fileBytes = GetBytesByFilePath(uri);
            }

            context.Response.ContentLength64 = fileBytes.Length;
            context.Response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
            ReCap.Server.Util.Logging.Log.Rest.Debug($"[RestClientAdapter] {context.Request.RawUrl} Success 200");
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
        try {
            return StaticStorageAdapter.GetFile(uri);
        }
        catch (FileNotFoundException)
        {
            return StaticStorageAdapter.GetFile((uri + "/index.html").Replace("//", "/"));
        }
    }

    private MethodInfo GetMethod(Type serviceType, string methodName)
    {
        MethodInfo[] methods = serviceType.GetMethods(BindingFlags.Instance | BindingFlags.Public);

        foreach (MethodInfo method in methods)
        {
            if (method.GetCustomAttribute(typeof(RequestMapping)) is RequestMapping mapping &&
                mapping.Name == methodName)
            {
                return method;
            }
        }

        throw new UnimplementedMethodException(methodName);
    }

    private MethodInfo GetExceptionMethod(Type exceptionType)
    {
        MethodInfo[] methods = typeof(GlobalExceptionHandler).GetMethods(BindingFlags.Static | BindingFlags.Public);

        foreach (MethodInfo method in methods)
        {
            if (method.GetCustomAttribute(typeof(ExceptionHandler)) is ExceptionHandler handler &&
                handler.Type == exceptionType)
            {
                return method;
            }
        }

        return exceptionType.BaseType is { } baseType
            ? GetExceptionMethod(baseType)
            : throw new InvalidOperationException($"No exception handler registered for {exceptionType}");
    }

    private bool isRestController(System.Type restControllerType, string apiPath)
    {
        var dnAttribute = restControllerType.GetCustomAttributes(typeof(RestController), true).FirstOrDefault() as RestController;
        if (dnAttribute != null)
        {
            return apiPath == dnAttribute.Value;
        }
        return false;
    }

    private string? GetContentType(System.Type restControllerType, MethodInfo methodInfo)
    {
        if (methodInfo.GetCustomAttribute(typeof(RequestMapping)) is RequestMapping mapping &&
            mapping.ContentType is { } contentType)
        {
            return contentType;
        }
        var dnAttribute = restControllerType.GetCustomAttributes(typeof(RestController), true).FirstOrDefault() as RestController;
        if (dnAttribute != null)
        {
            return dnAttribute.ContentType;
        }
        return null;
    }
}
