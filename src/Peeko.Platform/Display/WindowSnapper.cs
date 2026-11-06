using System.Runtime.InteropServices;

namespace Peeko.Platform.Display;

/// <summary>Zona tata letak, dalam pecahan area kerja layar (0–1).</summary>
public sealed record SnapZone(string Name, double X, double Y, double Width, double Height);

/// <summary>
/// Snap zones: mendeteksi jendela yang mulai/selesai diseret (WinEvent MOVESIZESTART/END, tanpa
/// polling di luar seretan) dan memindahkan jendela ke zona yang dipilih.
/// </summary>
/// <remarks>Dibuat dan di-Start dari thread UI (hook WINEVENT_OUTOFCONTEXT memakai message loop-nya).</remarks>
public sealed class WindowSnapper : IDisposable
{
    public static readonly SnapZone[] Zones =
    [
        new("Kiri", 0, 0, 0.5, 1),
        new("Kanan", 0.5, 0, 0.5, 1),
        new("Kiri ⅓", 0, 0, 1 / 3.0, 1),
        new("Tengah ⅓", 1 / 3.0, 0, 1 / 3.0, 1),
        new("Kanan ⅓", 2 / 3.0, 0, 1 / 3.0, 1),
        new("Kiri ⅔", 0, 0, 2 / 3.0, 1),
        new("Kanan ⅔", 1 / 3.0, 0, 2 / 3.0, 1),
        new("Penuh", 0, 0, 1, 1),
    ];

    private const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A, EVENT_SYSTEM_MOVESIZEEND = 0x000B;
    private const uint WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 2;

    private readonly WinEventDelegate _callback;
    private IntPtr _hook;

    public WindowSnapper()
    {
        _callback = OnEvent;
    }

    /// <summary>Jendela mulai diseret/diubah ukurannya.</summary>
    public event Action<IntPtr>? DragStarted;

    /// <summary>Seretan selesai.</summary>
    public event Action<IntPtr>? DragEnded;

    public bool IsRunning => _hook != IntPtr.Zero;

    public void Start()
    {
        if (IsRunning) return;
        _hook = SetWinEventHook(EVENT_SYSTEM_MOVESIZESTART, EVENT_SYSTEM_MOVESIZEEND, IntPtr.Zero, _callback, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }

    public void Stop()
    {
        if (!IsRunning) return;
        UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }

    private void OnEvent(IntPtr hook, uint type, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0 || hwnd == IntPtr.Zero) return; // OBJID_WINDOW saja
        if (type == EVENT_SYSTEM_MOVESIZESTART) DragStarted?.Invoke(hwnd);
        else if (type == EVENT_SYSTEM_MOVESIZEEND) DragEnded?.Invoke(hwnd);
    }

    /// <summary>Posisi kursor dalam piksel layar.</summary>
    public static (int X, int Y) Cursor()
    {
        GetCursorPos(out var p);
        return (p.X, p.Y);
    }

    /// <summary>
    /// Pindahkan jendela ke zona di layar tempat kursor berada. Bingkai tak terlihat Windows 10/11
    /// (bayangan ±7 px) dikompensasi supaya jendela rapat ke tepi dan ke jendela sebelahnya.
    /// </summary>
    public static bool Snap(IntPtr hwnd, SnapZone zone)
    {
        GetCursorPos(out var cursor);
        var monitor = MonitorFromPoint(cursor, 2 /* NEAREST */);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return false;
        var work = info.rcWork;
        int w = work.Right - work.Left, h = work.Bottom - work.Top;
        int x = work.Left + (int)Math.Round(zone.X * w);
        int y = work.Top + (int)Math.Round(zone.Y * h);
        int right = work.Left + (int)Math.Round((zone.X + zone.Width) * w);
        int bottom = work.Top + (int)Math.Round((zone.Y + zone.Height) * h);

        if (IsZoomed(hwnd)) ShowWindow(hwnd, 9 /* SW_RESTORE */);
        if (zone.Width >= 1 && zone.Height >= 1)
        {
            ShowWindow(hwnd, 3 /* SW_MAXIMIZE */);
            return true;
        }

        // Selisih antara GetWindowRect dan bingkai yang terlihat (DWMWA_EXTENDED_FRAME_BOUNDS).
        int l = 0, t = 0, r = 0, b = 0;
        if (GetWindowRect(hwnd, out var outer) && DwmGetWindowAttribute(hwnd, 9, out RECT visible, Marshal.SizeOf<RECT>()) == 0)
        {
            l = visible.Left - outer.Left;
            t = visible.Top - outer.Top;
            r = outer.Right - visible.Right;
            b = outer.Bottom - visible.Bottom;
        }
        const uint SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;
        return SetWindowPos(hwnd, IntPtr.Zero, x - l, y - t, right - x + l + r, bottom - y + t + b, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    public void Dispose() => Stop();

    private delegate void WinEventDelegate(IntPtr hook, uint type, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventDelegate callback, uint pid, uint thread, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT p, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int cmd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);
}
