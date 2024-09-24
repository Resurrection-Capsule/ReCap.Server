using System;
using System.IO;

namespace LoggerUtil;

public class Logger
{
    public static void debug(string value)
    {
        Console.WriteLine(value);
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
