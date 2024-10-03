using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using ReCap.Server.Adapters.Persistence.StaticStorage;
using ReCap.Server.Config.Sqlite;

using HttpServer;
using ReCap.Server.Utils.Logger;

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
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://*:80/");
        listener.Start();

        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            ProcessRequest(context);
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        var parameters = HttpUtils.GetParametersFromRequest(context.Request);
        if (parameters.Count > 0) {
            Logger.debug($"Parameters: {string.Join(", ", parameters)}");
        }

        string uri = context.Request.Url.LocalPath.Split("?")[0];
        byte[] fileBytes = null;

        try
        {
            foreach(object restController in restControllers)
            {
                var restControllerType = restController.GetType();
                if (isRestController(restControllerType, uri))
                {
                    var methodName = parameters.GetValueOrDefault("method", "<unknown>");
                    var method = GetMethod(restControllerType, methodName);
                    fileBytes = (byte[])method.Invoke(restController, new object[] { context, parameters });
                    context.Response.ContentType = GetContentType(restControllerType, method);

                    if (fileBytes == null)
                    {
                        throw new UnimplementedMethodException(methodName);
                    }
                }
            }
            
            if (fileBytes == null)
            {
                fileBytes = GetBytesByFilePath(uri);
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
        try {
            return StaticStorageAdapter.GetFile(uri);
        }
        catch (FileNotFoundException ex)
        {
            return StaticStorageAdapter.GetFile((uri + "/index.html").Replace("//", "/"));
        }
    }

    private MethodInfo GetMethod(Type serviceType, string methodName)
    {
        MethodInfo[] methods = serviceType.GetMethods(BindingFlags.Instance | BindingFlags.Public);

        foreach (MethodInfo method in methods)
        {
            if (method.GetCustomAttribute(typeof(RequestMapping)) != null &&
                ((RequestMapping)method.GetCustomAttribute(typeof(RequestMapping))).Name == methodName)
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
            return apiPath == dnAttribute.Value;
        }
        return false;
    }

    private string GetContentType(System.Type restControllerType, MethodInfo methodInfo)
    {
        if (methodInfo.GetCustomAttribute(typeof(RequestMapping)) != null)
        {
            var contentType = ((RequestMapping)methodInfo.GetCustomAttribute(typeof(RequestMapping))).ContentType;
            if (contentType != null)
            {
                return contentType;
            }
        }
        var dnAttribute = restControllerType.GetCustomAttributes(typeof(RestController), true).FirstOrDefault() as RestController;
        if (dnAttribute != null)
        {
            return dnAttribute.ContentType;
        }
        return null;
    }
}
