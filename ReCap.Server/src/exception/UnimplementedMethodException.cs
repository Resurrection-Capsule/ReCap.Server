using System;

public class UnimplementedMethodException : Exception
{
    public UnimplementedMethodException()
    {
    }

    public UnimplementedMethodException(string message)
        : base(message)
    {
    }

    public UnimplementedMethodException(string message, Exception inner)
        : base(message, inner)
    {
    }
}