using System.Runtime.InteropServices;
using static Celah.Platform.Native.NativeMethods;

namespace Celah.Platform.Display;

/// <summary>Operasi Win32 untuk jendela notch: gaya jendela, topmost, dan posisi.</summary>
public static class NotchWindowInterop
{
    /// <summary>
    /// Tidak muncul di taskbar/Alt-Tab (TOOLWINDOW) dan tidak mencuri fokus saat diklik (NOACTIVATE).
    /// </summary>
    public static void ApplyOverlayStyles(IntPtr hwnd)
    {
        var ex = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
        ex &= ~WS_EX_APPWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
    }

    /// <summary>Tegaskan ulang posisi paling atas (jendela topmost lain bisa menyalip).</summary>
    public static void EnsureTopmost(IntPtr hwnd)
    {
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    public static IntPtr GetPrimaryMonitor() =>
        MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);

    public static IntPtr GetMonitorOf(IntPtr hwnd) =>
        MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

    /// <summary>Letakkan jendela di tengah tepi atas monitor (koordinat piksel fisik).</summary>
    public static void PlaceTopCenter(IntPtr hwnd, IntPtr monitor)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info) || !GetWindowRect(hwnd, out var rect)) return;

        var m = info.rcMonitor;
        int x = m.Left + (m.Width - rect.Width) / 2;
        int y = m.Top;
        SetWindowPos(hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }
}
