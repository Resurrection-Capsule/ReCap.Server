using System;

namespace ReCap.Server;

public class ForbiddenOperationException : Exception
{
    public ForbiddenOperationException()
    {
    }

    public ForbiddenOperationException(string message)
        : base(message)
    {
    }

    public ForbiddenOperationException(string message, Exception inner)
        : base(message, inner)
    {
    }
}