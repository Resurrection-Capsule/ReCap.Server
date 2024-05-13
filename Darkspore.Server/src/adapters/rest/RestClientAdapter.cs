using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using HttpServer;

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

    private void ProcessRequest(HttpListenerContext context)
    {
        byte[] fileBytes = null;

        try
        {
            var query = context.Request.QueryString;
            string uri = context.Request.Url.LocalPath;

            if (uri == "/bootstrap/launcher/")
            {
                fileBytes = StaticStorageAdapter.GetFile("/bootstrap/launcher/wrapper.html");
            }
            else if (uri == "/bootstrap/launcher/notes")
            {
                fileBytes = StaticStorageAdapter.GetFile("/bootstrap/launcher/notes.html");
            }
            else if (uri.StartsWith("/recap/api"))
            {
                var method = GetMethod(typeof(ReCapRestClientAdapter), query.Get("method"));
                fileBytes = (byte[])method.Invoke(reCapRestClientAdapter, new object[] { context.Request });
            }
            else if (uri.StartsWith("/bootstrap/api"))
            {
                var method = GetMethod(typeof(BootstrapRestClientAdapter), query.Get("method"));
                fileBytes = (byte[])method.Invoke(bootstrapRestClientAdapter, new object[] { query });
                context.Response.ContentType = "text/xml";
            }
            else if (uri.StartsWith("/game/api"))
            {
                var method = GetMethod(typeof(GameRestClientAdapter), query.Get("method"));
                fileBytes = (byte[])method.Invoke(gameRestClientAdapter, new object[] { query });
                context.Response.ContentType = "text/xml";
            }
            else if (uri.StartsWith("/survey/api"))
            {
                var method = GetMethod(typeof(SurveyRestClientAdapter), query.Get("method"));
                fileBytes = (byte[])method.Invoke(surveyRestClientAdapter, new object[] { query });
                context.Response.ContentType = "text/xml";
            }
            else
            {
                fileBytes = StaticStorageAdapter.GetFile(uri);
            }

            if (fileBytes != null) {
                context.Response.ContentLength64 = fileBytes.Length;
                context.Response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
            }
            else {
                context.Response.StatusCode = 501;
                context.Response.StatusDescription = "Method not implemented";
            }
            context.Response.Close();
        }
        catch (BadRequestException ex)
        {
            context.Response.StatusCode = 400;
            context.Response.StatusDescription = ex.Message;
            context.Response.Close();
        }
        catch (ForbiddenOperationException ex)
        {
            context.Response.StatusCode = 403;
            context.Response.StatusDescription = ex.Message;
            context.Response.Close();
        }
        catch (FileNotFoundException ex)
        {
            context.Response.StatusCode = 404;
            context.Response.StatusDescription = "File not found: " + ex.Message;
            context.Response.Close();
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            context.Response.StatusDescription = "Error serving file: " + ex.Message;
            Console.WriteLine(ex.ToString());
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
