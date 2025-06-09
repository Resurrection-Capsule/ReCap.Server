using System;

namespace ReCap.Server;

public class ExceptionHandler : Attribute
{
    public Type? Type;
}
