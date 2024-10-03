using System;
using System.IO;

namespace ReCap.Server.Utils.Logger;

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
