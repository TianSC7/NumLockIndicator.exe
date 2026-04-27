using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NumLockIndicator;

internal sealed class MiddleButtonFilter : IDisposable
{
    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_MOUSE_LL = 14;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int HC_ACTION = 0;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly LowLevelMouseProc _proc;
    private long _lastTickMs;
    private int _thresholdMs;
    public bool IsEnabled { get; set; } = true;

    public void UpdateThreshold(int thresholdMs)
    {
        _thresholdMs = thresholdMs;
    }

    private readonly BlockingCollection<string> _logQueue = new(4096);
    private readonly Thread _logThread;
    private readonly StreamWriter _logWriter;
    private long _totalClicks;
    private long _totalBlocked;

    public MiddleButtonFilter(int thresholdMs = 200)
    {
        _thresholdMs = thresholdMs;
        var logDir = AppContext.BaseDirectory;
        var logPath = Path.Combine(logDir, "middlebutton.log");
        _logWriter = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8) { AutoFlush = true };

        _logThread = new Thread(FlushLoop) { IsBackground = true, Name = "MBFilterLogger" };
        _logThread.Start();

        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc,
                    GetModuleHandle(curModule.ModuleName!), 0);

        if (_hookId == IntPtr.Zero)
        {
            EnqueueLog("INIT | SetWindowsHookEx FAILED");
        }
        else
        {
            EnqueueLog($"INIT | Hook installed, threshold={_thresholdMs}ms (system DCT={GetDoubleClickTime()}ms)");
        }
    }

    private void EnqueueLog(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}";
        if (!_logQueue.TryAdd(line))
        {
            Debug.WriteLine("[MBFilter] log queue full, dropping entry");
        }
    }

    private void FlushLoop()
    {
        foreach (var line in _logQueue.GetConsumingEnumerable())
        {
            try { _logWriter.WriteLine(line); }
            catch { break; }
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION && IsEnabled && wParam == WM_MBUTTONDOWN)
        {
            long now = Environment.TickCount64;
            long interval = now - _lastTickMs;

            if (_lastTickMs != 0 && interval <= _thresholdMs)
            {
                Interlocked.Increment(ref _totalClicks);
                long blocked = Interlocked.Increment(ref _totalBlocked);
                EnqueueLog($"BLOCK | interval={interval}ms, threshold={_thresholdMs}ms | total_clicks={Interlocked.Read(ref _totalClicks)}, blocked={blocked}");
                return (IntPtr)1;
            }

            Interlocked.Increment(ref _totalClicks);
            EnqueueLog($"PASS  | interval={interval}ms, threshold={_thresholdMs}ms | total_clicks={Interlocked.Read(ref _totalClicks)}, blocked={Interlocked.Read(ref _totalBlocked)}");
            _lastTickMs = now;
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        EnqueueLog($"DISPOSE | total_clicks={Interlocked.Read(ref _totalClicks)}, blocked={Interlocked.Read(ref _totalBlocked)}");

        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        _logQueue.CompleteAdding();
        _logThread.Join(2000);
        _logWriter.Flush();
        _logWriter.Dispose();
        _logQueue.Dispose();
    }
}
