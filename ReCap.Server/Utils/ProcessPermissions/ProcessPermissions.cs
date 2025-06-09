using System.Diagnostics;

namespace ReCap.Server.Utils;

#nullable disable
public static class ProcessPermissions
{
    static readonly IProcessPermissionsImpl _permissions = PlatformUtils.GetForPlatform<IProcessPermissionsImpl>();


    /// <summary>
    /// Whether the current process has elevated permissions (Administrator on Windows, root on Linux and macOS)
    /// </summary>
    public static bool IsCurrentProcessElevated
    {
        get => _permissions.IsCurrentProcessElevated;
    }


    /// <summary>
    /// Attempts to relaunch the current process's executable with elevated permissions.
    /// &quot;Elevated permissions&quot; refers to Administrator privileges on Windows, root permissions on Linux and macOS
    /// </summary>
    /// <param name="args">Command-line options to pass to the new </param>
    /// <param name="elevatedProcess">If relaunch succeeded, outputs <see cref="Process"/> object representing the newly-created process.</param>
    /// <returns><see langword="true"/> if new instance was started successfully, <see langword="false"/> if relaunch failed or was cancelled by mandatory user authorizsation.</returns>
    public static bool TryRerunElevated(string args, out Process elevatedProcess)
        => TryRerunElevatedInternal(args, out elevatedProcess);

    /// <inheritdoc />
    public static bool TryRerunElevated(out Process elevatedProcess)
        => TryRerunElevatedInternal(null, out elevatedProcess);


    public static async Task<Process> RerunElevatedAsync(string args = null)
    {
        Process elevatedProcess = null;
        return await Task.Run(() => TryRerunElevatedInternal(args, out elevatedProcess))
            ? elevatedProcess
            : null
        ;
    }


    static bool TryRerunElevatedInternal(string args, out Process elevatedProcess)
        => _permissions.TryRerunElevated(
            args != null
                ? args
                : CommandLineUtils.GetCurrentProcessCommandLineArgs()
            , out elevatedProcess
        );
}
#nullable restore