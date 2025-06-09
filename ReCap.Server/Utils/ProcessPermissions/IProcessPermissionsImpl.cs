using System.Diagnostics;

namespace ReCap.Server.Utils;

#nullable disable
internal interface IProcessPermissionsImpl
{
    bool IsCurrentProcessElevated
    {
        get;
    }

    /// <summary>
    /// Attempts to start a new instance of the current process's executable with elevated permissions (Administrator on Windows, root on Linux and macOS)
    /// </summary>
    /// <param name="args">Command-line options to pass to the new </param>
    /// <param name="elevatedProcess"></param>
    /// <returns></returns>
    bool TryRerunElevated(string args, out Process elevatedProcess);
}
#nullable restore