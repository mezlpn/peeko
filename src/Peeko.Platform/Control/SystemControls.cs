// Diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): perintah Command Center
// (radio Wi-Fi/Bluetooth lewat Windows.Devices.Radios, volume, kecerahan, hemat baterai).

using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using Windows.Devices.Radios;

namespace Peeko.Platform.Control;

/// <summary>
/// Pengaturan cepat Windows untuk Command Center. Semua metode aman dipanggil dari thread latar dan
/// mengembalikan null/false jika perangkat tidak mendukung, bukan melempar exception.
/// </summary>
public static class SystemControls
{
    // ── Radio ──────────────────────────────────────────────────────────────

    /// <summary>Status radio (true = nyala), atau null jika perangkat tidak punya radio itu.</summary>
    public static async Task<bool?> GetRadioAsync(RadioKind kind)
    {
        var radio = await FindRadioAsync(kind);
        return radio is null ? null : radio.State == RadioState.On;
    }

    /// <returns>Status setelah diubah, atau null jika gagal (tidak ada radio / ditolak Windows).</returns>
    public static async Task<bool?> SetRadioAsync(RadioKind kind, bool on)
    {
        try
        {
            if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed) return null;
            var radio = await FindRadioAsync(kind);
            if (radio is null) return null;
            var result = await radio.SetStateAsync(on ? RadioState.On : RadioState.Off);
            return result == RadioAccessStatus.Allowed ? on : radio.State == RadioState.On;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<Radio?> FindRadioAsync(RadioKind kind)
    {
        try
        {
            foreach (var radio in await Radio.GetRadiosAsync())
                if (radio.Kind == kind) return radio;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    // ── Volume ─────────────────────────────────────────────────────────────

    /// <summary>Volume master perangkat output default (0–1) dan status bisu.</summary>
    public static (float Level, bool Muted)? GetVolume()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var v = device.AudioEndpointVolume;
            return (v.MasterVolumeLevelScalar, v.Mute);
        }
        catch (COMException)
        {
            return null;
        }
    }

    public static void SetVolume(float level)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var v = device.AudioEndpointVolume;
            v.MasterVolumeLevelScalar = Math.Clamp(level, 0f, 1f);
            if (level > 0 && v.Mute) v.Mute = false;
        }
        catch (COMException)
        {
        }
    }

    // ── Kecerahan (layar laptop, WMI) ──────────────────────────────────────

    /// <summary>Kecerahan 0–100, atau null untuk PC/monitor tanpa kontrol WMI.</summary>
    public static int? GetBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (ManagementObject o in searcher.Get())
                using (o) return Convert.ToInt32(o["CurrentBrightness"]);
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    public static void SetBrightness(int percent)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject o in searcher.Get())
                using (o) o.InvokeMethod("WmiSetBrightness", [(uint)1, (byte)Math.Clamp(percent, 0, 100)]);
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
        }
    }

    // ── Hemat baterai ──────────────────────────────────────────────────────

    /// <summary>Penghemat baterai Windows sedang aktif.</summary>
    public static bool IsEnergySaverOn() =>
        GetSystemPowerStatus(out var status) && status.SystemStatusFlag == 1;

    /// <summary>
    /// Baterai: persen, charger tercolok, benar-benar sedang mengisi, sisa waktu (bila Windows tahu).
    /// Null jika tanpa baterai. Tercolok belum tentu mengisi (mis. batas pengisian 80% aktif).
    /// </summary>
    public static (int Percent, bool PluggedIn, bool Charging, TimeSpan? Remaining)? GetBattery()
    {
        if (!GetSystemPowerStatus(out var s) || s.BatteryFlag == 128 || s.BatteryLifePercent > 100) return null;
        TimeSpan? remaining = s.BatteryLifeTime == uint.MaxValue ? null : TimeSpan.FromSeconds(s.BatteryLifeTime);
        return (s.BatteryLifePercent, s.ACLineStatus == 1, (s.BatteryFlag & 8) != 0, remaining);
    }

    // ── Pintasan ───────────────────────────────────────────────────────────

    /// <summary>Buka halaman Pengaturan Windows, mis. "ms-settings:batterysaver".</summary>
    public static void OpenSettings(string uri) => Open(uri);

    public static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    /// <summary>Pusat Tindakan/Notifikasi Windows (Win + A).</summary>
    public static void OpenActionCenter() => SendWinChord(0x41);

    /// <summary>Kunci layar (Win + L).</summary>
    public static void LockScreen() => LockWorkStation();

    private static void SendWinChord(byte key)
    {
        const byte VK_LWIN = 0x5B;
        const uint KEYUP = 0x0002;
        keybd_event(VK_LWIN, 0, 0, 0);
        keybd_event(key, 0, 0, 0);
        keybd_event(key, 0, KEYUP, 0);
        keybd_event(VK_LWIN, 0, KEYUP, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte vk, byte scan, uint flags, nint extra);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();
}
