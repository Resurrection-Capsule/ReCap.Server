using System.Diagnostics;

namespace ReCap.Server.Utils
{
    #nullable disable
    internal interface IProcessPermissionsImpl
    {
        bool IsCurrentProcessElevated
        {
            get;
        }

        bool TryRerunElevated(string args, out Process elevatedProcess);
    }


    public static class ProcessPermissions
    {
        static readonly IProcessPermissionsImpl _permissions = PlatformUtils.GetForPlatform<IProcessPermissionsImpl>();


        public static bool IsCurrentProcessElevated
        {
            get => _permissions.IsCurrentProcessElevated;
        }
        public static bool TryRerunElevated(string args, out Process elevatedProcess)
            => TryRerunElevatedInternal(args, out elevatedProcess);
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
}