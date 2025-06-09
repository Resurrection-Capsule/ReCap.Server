using System;
using System.Diagnostics;

namespace ReCap.Server.Utils;

public class Logger
{
    public static void debug(string value)
    {
        Debug.WriteLine(value);
    }

    public static void info(string value)
    {
        Console.WriteLine(value);
    }

    public static void error(string value)
    {
        Console.WriteLine(value);
    }
}
