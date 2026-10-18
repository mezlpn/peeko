using System.Diagnostics;
using System.Runtime.InteropServices;
using static Celah.Platform.Native.NativeMethods;

namespace Celah.Platform.Display;

/// <summary>
/// Mengamati jendela foreground di monitor tempat notch berada, tanpa polling, lalu melaporkan:
/// <list type="bullet">
/// <item><see cref="IsFullscreen"/>: aplikasi fullscreen (game, video, presentasi) → notch menyingkir.</item>
/// <item><see cref="CoversTop"/>: jendela menempel ke tepi atas di posisi notch (mis. tab Chrome
/// yang di-maximize) → notch menjadi garis tipis.</item>
/// </list>
/// Sumber event: pergantian foreground, minimize/restore, dan perubahan ukuran/posisi jendela
/// milik proses foreground (mis. video YouTube masuk fullscreen tanpa ganti jendela).
/// </summary>
/// <remarks>
/// Harus dibuat dan di-<see cref="Start"/> dari thread yang punya message loop (thread UI WPF),
/// karena hook WINEVENT_OUTOFCONTEXT dikirim lewat message loop thread pemasangnya.
/// </remarks>
public sealed class ForegroundWindowWatcher : IDisposable
{
    private static readonly HashSet<string> ShellClasses =
        ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    private readonly Func<IntPtr> _targetMonitor;
    private readonly uint _ownPid = (uint)Environment.ProcessId;

    // Delegate disimpan di field supaya tidak dibersihkan GC selama hook aktif.
    private readonly WinEventDelegate _globalCallback;
    private readonly WinEventDelegate _locationCallback;

    private IntPtr _globalHook;
    private IntPtr _minimizeHook;
    private IntPtr _locationHook;
    private uint _locationPid;

    /// <param name="targetMonitor">HMONITOR tempat notch berada.</param>
    public ForegroundWindowWatcher(Func<IntPtr> targetMonitor)
    {
        _targetMonitor = targetMonitor;
        _globalCallback = OnGlobalEvent;
        _locationCallback = OnLocationEvent;
    }

    public bool IsFullscreen { get; private set; }

    public bool CoversTop { get; private set; }

    public event Action<bool>? FullscreenChanged;

    public event Action<bool>? CoversTopChanged;

    /// <summary>Jendela foreground berganti (dipakai untuk menegaskan ulang topmost).</summary>
    public event Action? ForegroundChanged;

    public void Start()
    {
        _globalHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _globalCallback, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        _minimizeHook = SetWinEventHook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND,
            IntPtr.Zero, _globalCallback, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        TrackForegroundProcess(GetForegroundWindow());
        Evaluate();
    }

    /// <summary>Evaluasi ulang secara manual (mis. setelah resolusi/monitor berubah).</summary>
    public void Evaluate()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == _ownPid) return; // menu tray/jendela kita sendiri: pertahankan status sebelumnya

        var (fullscreen, coversTop) = Classify(hwnd);
        fullscreen |= IsShellReportingFullscreen();

        if (fullscreen != IsFullscreen)
        {
            IsFullscreen = fullscreen;
            FullscreenChanged?.Invoke(fullscreen);
        }
        if (coversTop != CoversTop)
        {
            CoversTop = coversTop;
            CoversTopChanged?.Invoke(coversTop);
        }
    }

    private (bool Fullscreen, bool CoversTop) Classify(IntPtr hwnd)
    {
        if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return default;
        if (hwnd == GetShellWindow() || hwnd == GetDesktopWindow()) return default;
        if (ShellClasses.Contains(GetClassName(hwnd))) return default;

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != _targetMonitor()) return default;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info) || !GetWindowRect(hwnd, out var rect)) return default;

        var m = info.rcMonitor;
        bool exact = rect.Left == m.Left && rect.Top == m.Top && rect.Right == m.Right && rect.Bottom == m.Bottom;

        // Jendela yang di-maximize meluber beberapa piksel di luar monitor (border). Kalau taskbar
        // auto-hide, ia menutupi seluruh layar walau bukan fullscreen, jadi dikecualikan.
        bool covers = rect.Left <= m.Left && rect.Top <= m.Top && rect.Right >= m.Right && rect.Bottom >= m.Bottom;
        bool fullscreen = exact || (covers && !IsZoomed(hwnd));

        // Tepi atas jendela berada di baris paling atas layar dan melintasi tengah layar (posisi notch).
        int centerX = m.Left + m.Width / 2;
        bool coversTop = rect.Top <= m.Top + 2 && rect.Left < centerX && rect.Right > centerX;

        return (fullscreen, coversTop && !fullscreen);
    }

    private static bool IsShellReportingFullscreen()
    {
        if (SHQueryUserNotificationState(out var state) != 0) return false;
        return state is QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_PRESENTATION_MODE;
    }

    private void OnGlobalEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (eventType == EVENT_SYSTEM_FOREGROUND)
        {
            TrackForegroundProcess(hwnd);
            ForegroundChanged?.Invoke();
        }
        Evaluate();
    }

    private void OnLocationEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // Event ini juga dikirim untuk caret/kursor; hanya jendela foreground yang relevan.
        if (idObject != OBJID_WINDOW || idChild != 0) return;
        if (hwnd != GetForegroundWindow()) return;
        Evaluate();
    }

    /// <summary>Pasang hook perubahan lokasi hanya untuk proses foreground, supaya tetap murah.</summary>
    private void TrackForegroundProcess(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == _ownPid || pid == _locationPid) return;

        if (_locationHook != IntPtr.Zero)
            UnhookWinEvent(_locationHook);

        _locationPid = pid;
        _locationHook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _locationCallback, pid, 0, WINEVENT_OUTOFCONTEXT);

        if (_locationHook == IntPtr.Zero)
            Debug.WriteLine($"ForegroundWindowWatcher: gagal memasang hook lokasi untuk PID {pid}");
    }

    public void Dispose()
    {
        if (_globalHook != IntPtr.Zero) UnhookWinEvent(_globalHook);
        if (_minimizeHook != IntPtr.Zero) UnhookWinEvent(_minimizeHook);
        if (_locationHook != IntPtr.Zero) UnhookWinEvent(_locationHook);
        _globalHook = _minimizeHook = _locationHook = IntPtr.Zero;
    }
}
