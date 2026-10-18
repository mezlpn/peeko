using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Celah.Platform.Power;

/// <summary>
/// Status baterai dan charger. Event utama dari <c>PowerModeChanged</c>; selama memakai baterai
/// ada pengecekan cadangan tiap menit karena tidak semua perangkat mengirim event per persen.
/// </summary>
public sealed class PowerWatcher : IDisposable
{
    private static readonly TimeSpan BatteryPoll = TimeSpan.FromMinutes(1);
    private readonly Timer _poll;

    public PowerWatcher()
    {
        _poll = new Timer(_ => Raise(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>(pakai charger?, persen, estimasi sisa waktu)</summary>
    public event Action<bool, int, TimeSpan?>? StatusChanged;

    public bool HasBattery { get; private set; }

    public void Start()
    {
        if (!GetSystemPowerStatus(out var s) || (s.BatteryFlag & 128) != 0 || s.BatteryLifePercent == 255)
        {
            HasBattery = false; // PC desktop
            return;
        }
        HasBattery = true;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Raise();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.StatusChange or PowerModes.Resume) Raise();
    }

    private void Raise()
    {
        if (!GetSystemPowerStatus(out var s)) return;
        bool onAc = s.ACLineStatus == 1;
        int percent = Math.Min(100, (int)s.BatteryLifePercent);
        TimeSpan? remaining = s.BatteryLifeTime is > 0 and < int.MaxValue ? TimeSpan.FromSeconds(s.BatteryLifeTime) : null;

        _poll.Change(onAc ? Timeout.InfiniteTimeSpan : BatteryPoll, onAc ? Timeout.InfiniteTimeSpan : BatteryPoll);
        StatusChanged?.Invoke(onAc, percent, remaining);
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _poll.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
}
