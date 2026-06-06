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