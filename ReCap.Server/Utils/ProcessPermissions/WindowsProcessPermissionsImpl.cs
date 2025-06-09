using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.Win32;

namespace ReCap.Server.Utils;

#nullable disable
[SupportedOSPlatform(nameof(OSPlatform.Windows))]
// https://github.com/Splitwirez/Spore-Mod-Manager/blob/development-pseudo-trunk/SporeMods.Core/UAC/Permissions%60Process.cs
internal class WindowsProcessPermissionsImpl
    : IProcessPermissionsImpl
{
    public bool IsCurrentProcessElevated
    {
        get => IsAtleastWindowsVista()
            ? _principal.IsInRole(WindowsBuiltInRole.Administrator)
            : true
        ;
    }
    public bool TryRerunElevated(string args, out Process elevatedProcess)
    {
        try
        {
            elevatedProcess = RerunAsAdministrator(args);
            return true;
        }
        catch (Exception)
        {
            elevatedProcess = default;
            return false;
        }
    }


    WindowsIdentity _identityCached = null;
    WindowsIdentity _identity
    {
        get
        {
            if (_identityCached == null)
                _identityCached = WindowsIdentity.GetCurrent();
            return _identityCached;
        }
    }

    WindowsPrincipal _principalCached = null;
    WindowsPrincipal _principal
    {
        get
        {
            if (_principalCached == null)
                _principalCached = new WindowsPrincipal(_identity);
            return _principalCached;
        }
    }

    bool IsAtleastWindowsVista()
        => PlatformInfo.OSVersion.Major >= 6;

    //https://stackoverflow.com/questions/1220213/detect-if-running-as-administrator-with-or-without-elevated-privileges
    //https://github.com/falahati/UACHelper
    /*bool IsExplicitlyElevated()
        => IsAdministrator() && _identity.Owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);*/

    const string _REGISTRY_ADDRESS = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";

    bool IsUACEnabled
    {
        get
        {
            if (!IsAtleastWindowsVista())
                return false;

            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = baseKey.OpenSubKey(_REGISTRY_ADDRESS, false))
            {
                return (key?.GetValue("EnableLUA", 0) as int? ?? 0) > 0;
            }
        }
    }


    Process RerunAsAdministrator(string args)
    {
        //https://stackoverflow.com/questions/133379/elevating-process-privilege-programmatically/10905713
        var exeName = Process.GetCurrentProcess().MainModule.FileName;
        Process process = null;
        ProcessStartInfo startInfo = new ProcessStartInfo(exeName, args)
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        process = Process.Start(startInfo);

        return process;
    }
}
#nullable restore