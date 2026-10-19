using System.Runtime.InteropServices;
using static Peeko.Platform.Native.NativeMethods;

namespace Peeko.Platform.Display;

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

    /// <summary>
    /// Sementara izinkan jendela menerima fokus keyboard (mis. mengetik to-do), lalu kembalikan
    /// ke mode tidak-mencuri-fokus.
    /// </summary>
    public static void SetActivatable(IntPtr hwnd, bool activatable)
    {
        var ex = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        ex = activatable ? ex & ~WS_EX_NOACTIVATE : ex | WS_EX_NOACTIVATE;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
    }

    /// <summary>Tegaskan ulang posisi paling atas (jendela topmost lain bisa menyalip).</summary>
    public static void EnsureTopmost(IntPtr hwnd)
    {
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    public const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

    /// <summary>Pintasan global Win + Alt + huruf. false jika sudah dipakai aplikasi lain.</summary>
    public static bool RegisterWinAltHotkey(IntPtr hwnd, int id, char key) =>
        RegisterHotKey(hwnd, id, MOD_WIN | MOD_ALT | MOD_NOREPEAT, char.ToUpperInvariant(key));

    public static void UnregisterHotkey(IntPtr hwnd, int id) => UnregisterHotKey(hwnd, id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static IntPtr GetPrimaryMonitor() =>
        MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);

    public static IntPtr GetMonitorOf(IntPtr hwnd) =>
        MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

    /// <summary>
    /// Letakkan jendela di tengah tepi atas monitor (koordinat piksel fisik), digeser
    /// <paramref name="offsetPx"/> ke kanan (negatif = kiri).
    /// </summary>
    public static void PlaceTopCenter(IntPtr hwnd, IntPtr monitor, int offsetPx = 0)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info) || !GetWindowRect(hwnd, out var rect)) return;

        var m = info.rcMonitor;
        int x = m.Left + (m.Width - rect.Width) / 2 + offsetPx;
        int y = m.Top;
        SetWindowPos(hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }
}
