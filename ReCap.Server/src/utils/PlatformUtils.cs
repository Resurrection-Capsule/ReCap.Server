using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReCap.Server.Utils;

#nullable disable
public static class PlatformUtils
{
    public static T GetForPlatform<T>()
    {
        Type iType = typeof(T);
        string implTypeName = InterfaceTypeNameToImplTypeName(iType.FullName);
        Type implType = iType.Assembly.GetType(implTypeName);
        return CreateInstance<T>(implType);
    }
    public static T GetForPlatform<T, TWindows, TLinux, TMacOS>()
        where TWindows : T, new()
        where TLinux : T, new()
        where TMacOS : T, new()
        => GetForPlatformInternal<T>(typeof(TWindows), typeof(TLinux), typeof(TMacOS));
    /*
    {
        if (PlatformInfo.IsWindows)
            return new TWindows();
        else if (PlatformInfo.IsLinux)
            return new TLinux();
        else if (PlatformInfo.IsMacOS)
            return new TMacOS();
        else
            throw new PlatformNotSupportedException();
    }
    */


    static T GetForPlatformInternal<T>(Type windowsImpl, Type linuxImpl, Type macOSImpl)
    {
        Type implType;
        if (PlatformInfo.IsWindows)
            implType = windowsImpl;
        else if (PlatformInfo.IsLinux)
            implType = linuxImpl;
        else if (PlatformInfo.IsMacOS)
            implType = macOSImpl;
        else
            throw new PlatformNotSupportedException();

        return CreateInstance<T>(implType);
    }


    static string InterfaceTypeNameToImplTypeName(string interfaceTypeFullName)
    {
        int namespaceEnd = interfaceTypeFullName.LastIndexOf('.') + 1;
        string typeNamespace = interfaceTypeFullName.Substring(0, namespaceEnd);

        string implTypeName;
        if (PlatformInfo.IsWindows)
            implTypeName = "Windows";
        else if (PlatformInfo.IsLinux)
            implTypeName = "Linux";
        else if (PlatformInfo.IsMacOS)
            implTypeName = "MacOS";
        else
            throw new PlatformNotSupportedException();
        return typeNamespace + implTypeName + interfaceTypeFullName.Substring(namespaceEnd + 1);
    }

    static T CreateInstance<T>(Type implType)
    {
        if (Activator.CreateInstance(implType) is T tImpl)
            return tImpl;
        else
            throw new InvalidCastException();
    }
}
#nullable restore