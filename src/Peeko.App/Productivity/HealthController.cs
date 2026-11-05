using System.Windows.Threading;
using Microsoft.Win32;
using Peeko.App.Settings;
using Peeko.Core.Peeks;
using Peeko.Core.Storage;
using Peeko.Platform.Input;

namespace Peeko.App.Productivity;

public sealed class HealthData
{
    public DateOnly Date { get; set; }
    public int Breaks { get; set; }
    public int EyeBreaks { get; set; }
    public int Breaths { get; set; }
}

/// <summary>
/// Pengingat istirahat gerak dan 20-20-20 untuk mata, plus hitungan harian (termasuk latihan napas).
/// Tanpa polling: satu timer sekali-jalan per pengingat. Saat timer jatuh tempo, Peeko melihat lama
/// sejak input terakhir: kalau kamu sedang tidak di depan laptop (≥ 5 menit), hitungan dimulai lagi
/// setelah kamu kembali, jadi pengingat hanya muncul setelah benar-benar lama bekerja.
/// </summary>
public sealed class HealthController : IDisposable
{
    private static readonly TimeSpan Away = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EyeInterval = TimeSpan.FromMinutes(20);

    private readonly SettingsService _settings;
    private readonly JsonFileStore<HealthData> _store;
    private readonly AutoSaver _saver;
    private readonly HealthData _d;
    private readonly DispatcherTimer _break = new(DispatcherPriority.Background);
    private readonly DispatcherTimer _eyes = new(DispatcherPriority.Background);

    public HealthController(SettingsService settings)
    {
        _settings = settings;
        _store = new JsonFileStore<HealthData>(App.DataDirectory, "health.json");
        _d = _store.Load() ?? new HealthData();
        _saver = new AutoSaver(() => _store.Save(_d));
        _break.Tick += (_, _) => Due(_break, BreakInterval, () =>
        {
            Count(d => d.Breaks++);
            PeekRequested?.Invoke(new PeekRequest(PeekKind.MoveBreak) { Detail = "Berdiri dan regangkan badan sebentar" });
        });
        _eyes.Tick += (_, _) => Due(_eyes, EyeInterval, () =>
        {
            Count(d => d.EyeBreaks++);
            PeekRequested?.Invoke(new PeekRequest(PeekKind.EyeBreak) { Detail = "Lihat sejauh 6 meter selama 20 detik" });
        });
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public event Action? Updated;

    public event Action<PeekRequest>? PeekRequested;

    private TimeSpan BreakInterval => TimeSpan.FromMinutes(_settings.Current.BreakIntervalMinutes);

    public int Breaks => Today.Breaks;
    public int EyeBreaks => Today.EyeBreaks;
    public int Breaths => Today.Breaths;

    private HealthData Today
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (_d.Date != today)
            {
                _d.Date = today;
                _d.Breaks = _d.EyeBreaks = _d.Breaths = 0;
            }
            return _d;
        }
    }

    /// <summary>Pengaturan berubah: nyalakan/matikan dan mulai hitungan baru.</summary>
    public void Apply()
    {
        var s = _settings.Current;
        Restart(_break, s.BreaksEnabled, BreakInterval);
        Restart(_eyes, s.EyeBreaksEnabled, EyeInterval);
        Updated?.Invoke();
    }

    /// <summary>Satu sesi latihan napas selesai.</summary>
    public void BreathDone() => Count(d => d.Breaths++);

    private void Count(Action<HealthData> change)
    {
        change(Today);
        _saver.MarkDirty();
        Updated?.Invoke();
    }

    private static void Restart(DispatcherTimer timer, bool enabled, TimeSpan interval)
    {
        timer.Stop();
        if (!enabled) return;
        timer.Interval = interval;
        timer.Start();
    }

    /// <summary>
    /// Waktunya mengingatkan. Sedang tidak di depan laptop: tunda dan cek lagi tiap menit sampai ada
    /// input, lalu mulai hitungan baru (yang tadi sudah istirahat sendiri).
    /// </summary>
    private void Due(DispatcherTimer timer, TimeSpan interval, Action remind)
    {
        if (IdleTime.Get() >= Away)
        {
            timer.Interval = TimeSpan.FromMinutes(1);
            timer.Tag = "away";
            return;
        }
        if (timer.Tag is "away")
        {
            timer.Tag = null;
            timer.Interval = interval;
            return;
        }
        timer.Interval = interval;
        // Layar penuh (presentasi, game) diatur NotchController lewat aturan peek biasa.
        remind();
    }

    // Layar dikunci lalu dibuka lagi: itu sudah istirahat.
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason != SessionSwitchReason.SessionUnlock) return;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(Apply);
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _break.Stop();
        _eyes.Stop();
        _saver.Flush();
    }
}
