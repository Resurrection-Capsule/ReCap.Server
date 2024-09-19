using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

DarksporeLauncher.Launch();

class DarksporeLauncher
{
    const int PROCESS_ALL_ACCESS = 0x1F0FFF;

    const int DARKSPORE_EXE_OFFSET = 0x400C00;

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

        {
            byte[] buff = new byte[size];
            int read = 0;
            ReadProcessMemory(hProcess, address, buff, size, ref read);
            Console.WriteLine($"OverwriteMemory ORIGINAL: 0x{address:X8} = {BitConverter.ToString(buff).Replace("-","")}");
        }

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

    public static void Launch()
    {
        // Create the event, so the game doesn't close itself instantly
        var eventHandle = CreateEvent(IntPtr.Zero, false, false, "Global\\Darkspore L2G");

        var process = Process.Start("Darkspore.exe");

        nint handle = OpenProcess(PROCESS_ALL_ACCESS, false, process.Id);

        var localhost = Encoding.ASCII.GetBytes("localhost\0");

        // // Override redirector hostname 1
        // if (!OverwriteMemory(handle, 0x12E2AD8, localhost, localhost.Length))
        // {
        //     Console.WriteLine($"ERROR: Unable to overwrite redirector hostname 1!");
        //     return;
        // }

        // // Override redirector hostname 2
        // if (!OverwriteMemory(handle, 0x12E2AF0, localhost, localhost.Length))
        // {
        //     Console.WriteLine($"ERROR: Unable to overwrite redirector hostname 2!");
        //     return;
        // }

        // // Override redirector hostname 3
        // if (!OverwriteMemory(handle, 0x12E2B0C, localhost, localhost.Length))
        // {
        //     Console.WriteLine($"ERROR: Unable to overwrite redirector hostname 3!");
        //     return;
        // }

        // // Override redirector hostname 4
        // if (!OverwriteMemory(handle, 0x12E2B28, localhost, localhost.Length))
        // {
        //     Console.WriteLine($"ERROR: Unable to overwrite redirector hostname 4!");
        //     return;
        // }

        // Disable secure connection for redirector (it's local anyways)
        var insecureRedirector = new byte[] { 0x01 };
        if (!OverwriteMemory(handle, DARKSPORE_EXE_OFFSET + 0xA4CF9D, insecureRedirector, insecureRedirector.Length))
        {
            Console.WriteLine($"ERROR: Unable to overwrite redirector secure bool param!");
            return;
        }

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