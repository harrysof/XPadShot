using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace XPadShot.Services;

/// <summary>
/// Watches the foreground app (used to name screenshots and to prefill new
/// profile names) and exposes the foreground window's process + title.
/// </summary>
public sealed class ForegroundWatcher : IDisposable
{
    private const int PollIntervalMs = 400;

    private readonly Thread _thread;
    private volatile bool _running = true;

    private string? _currentProcess;
    private string? _currentTitle;
    private string? _lastExternalProcess;
    private string? _lastExternalTitle;
    private string? _notifiedProcess;
    private volatile bool _haveValue;

    public event Action<string?>? ForegroundChanged;

    public ForegroundWatcher()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "foreground-poll" };
        _thread.Start();
    }

    /// <summary>Process name (no extension) of the foreground app, or null.</summary>
    public string? CurrentProcess => _currentProcess;

    /// <summary>
    /// Process + window title to label a capture with. Uses the current
    /// foreground app, or the last non-XPadShot app when the tool itself
    /// (or its dialog) is focused.
    /// </summary>
    public (string? Process, string? Title) GetCaptureTarget()
    {
        if (_currentProcess is not null && !IsSelf(_currentProcess))
            return (_currentProcess, _currentTitle);

        return (_lastExternalProcess, _lastExternalTitle);
    }

    private static bool IsSelf(string process) =>
        process.Equals("XPadShot", StringComparison.OrdinalIgnoreCase);

    private void Loop()
    {
        while (_running)
        {
            var (process, title) = GetForegroundInfo();

            if (process is not null && !IsSelf(process))
            {
                _lastExternalProcess = process;
                _lastExternalTitle = title;
            }

            _currentProcess = process;
            _currentTitle = title;

            if (!_haveValue || !string.Equals(process, _notifiedProcess, StringComparison.OrdinalIgnoreCase))
            {
                _haveValue = true;
                _notifiedProcess = process;
                ForegroundChanged?.Invoke(process);
            }

            Thread.Sleep(PollIntervalMs);
        }
    }

    /// <summary>Foreground window's process name and title.</summary>
    public static (string? Process, string? Title) GetForegroundInfo()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return (null, null);

        _ = GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0)
            return (null, null);

        string? process = null;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            process = p.ProcessName;
        }
        catch
        {
            // protected process
        }

        string? title = null;
        try
        {
            int length = GetWindowTextLengthW(hwnd);
            if (length > 0)
            {
                var buffer = new StringBuilder(length + 2);
                _ = GetWindowTextW(hwnd, buffer, buffer.Capacity);
                var text = buffer.ToString().Trim();
                title = text.Length > 0 ? text : null;
            }
        }
        catch
        {
            // no caption
        }

        return (process, title);
    }

    public void Dispose() => _running = false;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLengthW(IntPtr hWnd);
}
