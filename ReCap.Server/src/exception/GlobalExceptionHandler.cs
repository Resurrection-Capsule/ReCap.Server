namespace ReCap.Server.Adapters.Rest.Api;

using System.Net;

using ReCap.Server.Utils.Logger;

public class GlobalExceptionHandler
{
    [ExceptionHandler(Type=typeof(BadRequestException))]
    public static void handleBadRequestException(HttpListenerContext context, BadRequestException ex)
    {
        context.Response.StatusCode = 400;
        context.Response.StatusDescription = ex.Message;
        context.Response.Close();
        Logger.error($"[RestClientAdapter] {context.Request.RawUrl} Error 400: {context.Response.StatusDescription}");
    }

    [ExceptionHandler(Type=typeof(ForbiddenOperationException))]
    public static void handleForbiddenOperationException(HttpListenerContext context, ForbiddenOperationException ex)
    {
        context.Response.StatusCode = 403;
        context.Response.StatusDescription = ex.Message;
        context.Response.Close();
        Logger.error($"[RestClientAdapter] {context.Request.RawUrl} Error 403: {context.Response.StatusDescription}");
    }

    [ExceptionHandler(Type=typeof(FileNotFoundException))]
    public static void handleForbiddenOperationException(HttpListenerContext context, FileNotFoundException ex)
    {
        context.Response.StatusCode = 404;
        context.Response.StatusDescription = "File not found: " + ex.Message;
        context.Response.Close();
        Logger.error($"[RestClientAdapter] {context.Request.RawUrl} Error 404: {context.Response.StatusDescription}");
    }

    [ExceptionHandler(Type=typeof(UnimplementedMethodException))]
    public static void handleUnimplementedMethodException(HttpListenerContext context, UnimplementedMethodException ex)
    {
        context.Response.StatusCode = 501;
        context.Response.StatusDescription = "Method not implemented";
        Logger.error($"[RestClientAdapter] {context.Request.RawUrl} Error 501: {ex.Message}");
        context.Response.Close();
    }

    [ExceptionHandler(Type=typeof(Exception))]
    public static void handleException(HttpListenerContext context, Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.StatusDescription = "Error serving file: " + ex.Message;
        Logger.error($"[RestClientAdapter] {context.Request.RawUrl} Error 500: {ex.ToString()}");
        context.Response.Close();
    }
}