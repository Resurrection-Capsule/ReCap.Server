using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using NDesk.Options;

class DarksporeLauncher
{
    const int PROCESS_ALL_ACCESS = 0x1F0FFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpBaseAddress, int dwSize, uint flNewProtect, out uint lpflOldProtect);

    public const int PAGE_READWRITE = 0x40;
    public const int PROCESS_VM_OPERATION = 0x0008;
    public const int PROCESS_VM_READ = 0x0010;
    public const int PROCESS_VM_WRITE = 0x0020;

    private static bool OverwriteMemory(nint hProcess, nint address, byte[] buffer, int size)
    {
        if (!VirtualProtectEx(hProcess, address, size, 0x40, out uint oldProtect))
        {
            Console.WriteLine($"OverwriteMemory ERROR: VirtualProtectEx failed: {Marshal.GetLastPInvokeError()} {Marshal.GetLastPInvokeErrorMessage()}");
            return false;
        }

        /*{
            byte[] buff = new byte[size];
            int read = 0;
            ReadProcessMemory(hProcess, address, buff, size, ref read);
            Console.WriteLine($"OverwriteMemory ORIGINAL: 0x{address:X8} = {BitConverter.ToString(buff).Replace("-","")}");
            Console.WriteLine($"OverwriteMemory ORIGINAL: 0x{address:X8} = {Encoding.ASCII.GetString(buff)}");
            Console.WriteLine("");
        }*/

        bool writeSuccess = true;

        int written = 0;
        if (!WriteProcessMemory(hProcess, address, buffer, size, ref written) || written != size)
        {
            Console.WriteLine($"OverwriteMemory ERROR: WriteProcessMemory failed: {Marshal.GetLastPInvokeError()} {Marshal.GetLastPInvokeErrorMessage()}");

            writeSuccess = false;
        }

        /*if (writeSuccess)
        {
            byte[] buff = new byte[size];
            int read = 0;
            ReadProcessMemory(hProcess, address, buff, size, ref read);
            Console.WriteLine($"OverwriteMemory CHECK: 0x{address:X8} = {Encoding.ASCII.GetString(buff)}");
        }*/

        if (!VirtualProtectEx(hProcess, address, size, oldProtect, out _))
        {
            Console.WriteLine($"OverwriteMemory ERROR: VirtualProtectEx RESTORE failed: {Marshal.GetLastPInvokeError()} {Marshal.GetLastPInvokeErrorMessage()}");
            return false;
        }

        return writeSuccess;
    }

    private static void OverwriteMemory(nint hProcess, string ogValue, nint address, byte[] buffer)
    {
        if (!OverwriteMemory(hProcess, address, buffer, buffer.Length)) {
            Console.WriteLine($"ERROR: Unable to overwrite {ogValue}!");
        }
    }

    public static void Main(string[] args)
    {
        var domain = "localhost"; // max: 17 characters
        var exePath = "Darkspore.exe";
        var showHelp = false;

        var p = new OptionSet () {
            { "d|domain", v => { if (v != null) domain = v; } },
            { "h|?|help", v => { showHelp = v != null; } },
            { "e|exe=",   v => { exePath = v; } },
        };

        List<string> extra;
        try {
            extra = p.Parse (args);
        }
        catch (OptionException e) {
            Console.WriteLine("Try `greet --help` for more information.");
            return;
        }

        if (showHelp) {
            Console.WriteLine("Use `--domain' to specify the domain, and --exe to specify the Darkspore EXE path.");
            return;
        }

        // Create the event, so the game doesn't close itself instantly
        var eventHandle = CreateEvent(IntPtr.Zero, false, false, "Global\\Darkspore L2G");

        var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
        string version = versionInfo.FileVersion;
        Console.WriteLine($"Darkspore {version} has been detected!");

        var process = Process.Start(exePath);

        nint handle = OpenProcess(PROCESS_ALL_ACCESS, false, process.Id);

        var localhost = Encoding.ASCII.GetBytes($"{domain}\0");

        // Override bootstrap API URL (config.darkspore.com)
        var extraChars = String.Concat(Enumerable.Repeat("A", 17 - domain.Length));
        var bootstrapApi = Encoding.ASCII.GetBytes($"http://{domain}/bootstrap/api?version=1&z={extraChars}");
        OverwriteMemory(handle, "http://config.darkspore.com/bootstrap/api?version=1", 0x401200 + 0xBD9A9C, bootstrapApi);

        OverwriteMemory(handle, "content.darkspore.com",       0x401200 + 0xBDA678, localhost);
        OverwriteMemory(handle, "api.darkspore.com",           0x401200 + 0xBDA690, localhost);
        OverwriteMemory(handle, "gosredirector.ea.com",        0x401200 + 0xCD887C, localhost);
        OverwriteMemory(handle, "gosredirector.scert.ea.com",  0x401200 + 0xCD8894, localhost);
        OverwriteMemory(handle, "gosredirector.stest.ea.com",  0x401200 + 0xCD88B0, localhost);
        OverwriteMemory(handle, "gosredirector.online.ea.com", 0x401200 + 0xCD88CC, localhost);

        // Disable secure connection for redirector (it's local anyways)
        var insecureRedirector = new byte[] { 0x01 };
        OverwriteMemory(handle, "redirector secure bool param", 0x400C00 + 0xA4CF9D, insecureRedirector);

        CloseHandle(handle);

        // TODO: (alt: enable SSL without certificate at all, so no fake certs need to be created)
        // TODO: handle anything else that needs override: verify cert? telemetry IP? disable telemetry alltogether?

        try
        {
            if (WaitForSingleObject(eventHandle, 15000) == 0)
                Console.WriteLine("Darkspore has opened!");
            else
                Console.WriteLine("Error opening Darkspore!");
        }
        finally
        {
            CloseHandle(eventHandle);
        }
    }
}