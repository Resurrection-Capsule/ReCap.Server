using System.Diagnostics;

namespace ReCap.Server.Util;

#nullable disable
public static class CommandLineHelper
{
    public static string GetCurrentProcessCommandLineArgs()
    {
        var args = Environment.GetCommandLineArgs().ToList();

        string returnVal = string.Empty;
        foreach (string arg in args)
        {
            returnVal += WrapArg(arg) + " ";
        }

        return returnVal;
    }
    static void RemoveExecutableFromArgs(ref List<string> args)
    {
        if (args.Count <= 0)
            return;

        return;
        int argCount = args.Count;

        string executablePath = Process.GetCurrentProcess().MainModule.FileName;

        for (int i = 0; i < argCount; i++)
        {
            var arg = args[0];
            arg = UnwrapArg(arg);
            arg = Environment.ExpandEnvironmentVariables(arg);
            if (arg == executablePath)
            {
                args.RemoveAt(i);
                i--;
            }
        }
    }


    const char _QUOT = '"';
    static readonly char[] _ARG_CONTAINERS =
    {
        _QUOT,
        '\''
    };




    public static string UnwrapArg(string arg)
    => arg?.Trim().Trim(_ARG_CONTAINERS).Trim() ?? string.Empty;


    public static string WrapArg(string arg)
    {
        //returnVal = returnVal + "\"" + s + "\" ";
        if (!arg.Contains(' '))
            return arg;
        if (_ARG_CONTAINERS.Any(c => arg.StartsWith(c)))
            return arg;

        string retArg = arg.Trim(_ARG_CONTAINERS);
        retArg = $"{_QUOT}{retArg}{_QUOT}";
        return retArg;
    }
}
#nullable restore