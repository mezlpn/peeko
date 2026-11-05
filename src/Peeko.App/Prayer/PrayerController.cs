using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Peeko.App.Settings;
using Peeko.Core.Peeks;
using Peeko.Core.Prayer;
using Peeko.Core.Status;

namespace Peeko.App.Prayer;

/// <summary>
/// Jadwal salat hari ini, pengingat, hitung mundur di pil, dan azan. Tidak ada polling: satu timer
/// sekali-jalan ke kejadian berikutnya (pengingat, mulai hitung mundur, masuk waktu, tengah malam), plus
/// tick per menit hanya selama hitung mundurnya terlihat.
/// </summary>
public sealed class PrayerController : IDisposable
{
    private readonly SettingsService _settings;
    private readonly Today.WeatherService _location;
    private readonly ILogger<PrayerController> _log;
    private readonly DispatcherTimer _next = new(DispatcherPriority.Normal);
    private readonly DispatcherTimer _minute = new(DispatcherPriority.Background);
    private readonly Dictionary<DateOnly, IReadOnlyList<PrayerTime>> _cache = [];
    private MediaPlayer? _adhan;
    private GeoPoint? _at;
    private bool _displayVisible;
    // Pengingat yang sudah dikirim, supaya tidak muncul dua kali setelah dijadwal ulang.
    private DateTime _remindedFor, _announcedFor;

    public PrayerController(SettingsService settings, Today.WeatherService location, ILogger<PrayerController> log)
    {
        _settings = settings;
        _location = location;
        _log = log;
        _next.Tick += (_, _) =>
        {
            _next.Stop();
            Evaluate();
        };
        _minute.Tick += (_, _) =>
        {
            _minute.Interval = UntilNextMinute();
            Updated?.Invoke();
        };
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
        location.LocationChanged += () => _ = LoadLocationAsync();
    }

    /// <summary>Jadwal, salat berikutnya, atau status hitung mundur berubah.</summary>
    public event Action? Updated;

    public event Action<PeekRequest>? PeekRequested;

    public bool HasLocation => _at is not null;

    public string? Place => _at?.City;

    public bool IsEnabled => _settings.Current.PrayerEnabled;

    /// <summary>Hitung mundur pil sedang berlaku (salat berikutnya tinggal ≤ N menit).</summary>
    public bool CountdownActive { get; private set; }

    public bool AdhanPlaying => _adhan is not null;

    public async Task StartAsync() => await LoadLocationAsync();

    private async Task LoadLocationAsync()
    {
        _at = await _location.ResolveLocationAsync();
        _cache.Clear();
        if (_at is null) _log.LogInformation("Jadwal salat: lokasi belum diketahui");
        Evaluate();
    }

    /// <summary>Pengaturan berubah (aktif, koreksi menit, pengingat).</summary>
    public void Apply()
    {
        _cache.Clear();
        if (!IsEnabled) StopAdhan();
        Evaluate();
    }

    public IReadOnlyList<PrayerTime> Today() => ScheduleFor(DateOnly.FromDateTime(DateTime.Now));

    public PrayerTime? Next() => _at is null ? null : PrayerTimes.Next(DateTime.Now, ScheduleFor);

    private IReadOnlyList<PrayerTime> ScheduleFor(DateOnly day)
    {
        if (_at is null) return [];
        if (_cache.TryGetValue(day, out var cached)) return cached;
        if (_cache.Count > 4) _cache.Clear();
        var offset = TimeZoneInfo.Local.GetUtcOffset(day.ToDateTime(new TimeOnly(12, 0)));
        int shift = _settings.Current.PrayerOffsetMinutes;
        var times = PrayerTimes.For(day, _at.Latitude, _at.Longitude, offset)
            .Select(t => t with { At = t.At.AddMinutes(shift) })
            .ToList();
        _cache[day] = times;
        return times;
    }

    /// <summary>Tick per menit hanya saat tab Salat atau pil hitung mundur terlihat.</summary>
    public void SetDisplayVisible(bool visible)
    {
        _displayVisible = visible;
        UpdateMinuteTick();
    }

    private void UpdateMinuteTick()
    {
        bool tick = _displayVisible && IsEnabled && _at is not null;
        if (tick && !_minute.IsEnabled)
        {
            _minute.Interval = UntilNextMinute();
            _minute.Start();
        }
        else if (!tick) _minute.Stop();
    }

    /// <summary>Selaras dengan pergantian menit supaya "12 mnt" berganti tepat waktu.</summary>
    private static TimeSpan UntilNextMinute()
    {
        var now = DateTime.Now;
        return TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond) + TimeSpan.FromMilliseconds(50);
    }

    /// <summary>Kirim kejadian yang sudah jatuh tempo, lalu jadwalkan kejadian berikutnya.</summary>
    private void Evaluate()
    {
        _next.Stop();
        var s = _settings.Current;
        if (!s.PrayerEnabled || _at is null)
        {
            CountdownActive = false;
            UpdateMinuteTick();
            Updated?.Invoke();
            return;
        }

        var now = DateTime.Now;
        // Baru saja masuk waktu (≤ 2 menit, mis. timer sedikit telat atau bangun dari sleep): umumkan.
        var current = Today().LastOrDefault(p => p.IsPrayer && p.At <= now);
        if (current is not null && now - current.At < TimeSpan.FromMinutes(2) && _announcedFor != current.At)
            Announce(current);

        var next = PrayerTimes.Next(now, ScheduleFor);
        var left = next.At - now;
        if (s.PrayerReminderMinutes > 0 && left <= TimeSpan.FromMinutes(s.PrayerReminderMinutes) && _remindedFor != next.At)
        {
            _remindedFor = next.At;
            int minutes = Math.Max(1, (int)Math.Ceiling(left.TotalMinutes));
            PeekRequested?.Invoke(new PeekRequest(PeekKind.PrayerSoon) { Text = next.Label, Detail = $"{minutes} menit lagi · {next.At:HH.mm}" });
        }
        CountdownActive = s.PrayerCountdownMinutes > 0 && left <= TimeSpan.FromMinutes(s.PrayerCountdownMinutes);

        // Kejadian berikutnya yang paling dekat.
        var candidates = new List<DateTime> { next.At, DateTime.Today.AddDays(1).AddSeconds(5) };
        if (s.PrayerReminderMinutes > 0) candidates.Add(next.At.AddMinutes(-s.PrayerReminderMinutes));
        if (s.PrayerCountdownMinutes > 0) candidates.Add(next.At.AddMinutes(-s.PrayerCountdownMinutes));
        var wake = candidates.Where(c => c > now).Min();
        _next.Interval = wake - now + TimeSpan.FromMilliseconds(200);
        _next.Start();

        UpdateMinuteTick();
        Updated?.Invoke();
    }

    private void Announce(PrayerTime prayer)
    {
        _announcedFor = prayer.At;
        PeekRequested?.Invoke(new PeekRequest(PeekKind.PrayerTime) { Text = prayer.Label, Detail = prayer.At.ToString("HH.mm") });
        var s = _settings.Current;
        if (s.AdhanEnabled && s.AdhanPath is { } path && File.Exists(path)) PlayAdhan(path);
    }

    /// <summary>Peek dan azan diredam saat Jangan ganggu (dipanggil NotchController).</summary>
    public Func<bool>? IsQuiet { get; set; }

    private void PlayAdhan(string path)
    {
        if (IsQuiet?.Invoke() == true) return;
        StopAdhan();
        try
        {
            var player = new MediaPlayer();
            player.MediaEnded += (_, _) => StopAdhan();
            player.MediaFailed += (_, e) =>
            {
                _log.LogWarning(e.ErrorException, "Azan gagal diputar");
                StopAdhan();
            };
            player.Open(new Uri(path));
            player.Play();
            _adhan = player;
            Updated?.Invoke();
        }
        catch (Exception ex) when (ex is UriFormatException or InvalidOperationException)
        {
            _log.LogWarning(ex, "Azan gagal diputar");
        }
    }

    /// <summary>Contoh dari Pengaturan: putar file azan sekarang.</summary>
    public void PreviewAdhan(string path)
    {
        if (File.Exists(path)) PlayAdhan(path);
    }

    public void StopAdhan()
    {
        if (_adhan is null) return;
        _adhan.Stop();
        _adhan.Close();
        _adhan = null;
        Updated?.Invoke();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) System.Windows.Application.Current.Dispatcher.BeginInvoke(Evaluate);
    }

    private void OnTimeChanged(object? sender, EventArgs e) =>
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            TimeZoneInfo.ClearCachedData();
            _cache.Clear();
            Evaluate();
        });

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
        _next.Stop();
        _minute.Stop();
        StopAdhan();
    }
}
