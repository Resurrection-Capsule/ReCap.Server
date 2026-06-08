using Serilog.Events;

namespace ReCap.Server.Util.Logging;

/// <summary>
/// Optional bridge that surfaces the ReCap.WebKit DLL's log (written inside Darkspore.exe, a
/// separate process) into the server's own log under the <c>WebKit</c> category — no coupling,
/// no dependency on the DLL: it just tails the file. Off by default; enable with
/// <c>--log-level=WebKit:debug</c>. The DLL appends crash-safely (flush per line), so the tail
/// only ever reads complete lines.
/// </summary>
public static class WebKitLogTail
{
    /// <summary>Default DLL log location relative to the game root: DarksporeBin\ReCapWebKit\ReCap.WebKit.log.</summary>
    public static string DefaultPath(string gameRoot)
        => Path.Combine(gameRoot, "DarksporeBin", "ReCapWebKit", "ReCap.WebKit.log");

    public static void Start(string path, CancellationToken token)
        => _ = Task.Run(() => RunAsync(path, token), token);

    static async Task RunAsync(string path, CancellationToken token)
    {
        try
        {
            // The game may not be running yet — wait for the DLL to create the file.
            while (!File.Exists(path) && !token.IsCancellationRequested)
                await Task.Delay(1000, token);
            if (token.IsCancellationRequested) return;

            // FileShare.ReadWrite: the DLL holds the file open for append the whole time.
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            fs.Seek(0, SeekOrigin.End);   // tail from here — skip lines from earlier sessions

            while (!token.IsCancellationRequested)
            {
                string? line;
                var read = false;
                while ((line = await reader.ReadLineAsync(token)) is not null)
                {
                    read = true;
                    if (line.Length > 0)
                        Log.WebKit.Debug(line);
                }
                if (!read)
                    await Task.Delay(250, token);   // idle poll for new appends
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            Log.Server.Warn($"WebKit log tail stopped: {ex.Message}");
        }
    }
}
