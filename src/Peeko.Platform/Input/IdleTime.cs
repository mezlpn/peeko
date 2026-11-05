using System.Runtime.InteropServices;

namespace Peeko.Platform.Input;

/// <summary>Lama sejak input terakhir (keyboard/mouse/sentuh) di sesi ini, tanpa hook atau polling.</summary>
public static class IdleTime
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static TimeSpan Get()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        // Keduanya hitungan 32-bit sejak boot; selisih unsigned tetap benar walau sudah berputar.
        uint ms = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(ms);
    }
}
