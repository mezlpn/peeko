namespace Peeko.Core.Status;

public sealed class ScreenTimeData
{
    /// <summary>Tanggal (yyyy-MM-dd) → nama aplikasi → detik di depan.</summary>
    public Dictionary<string, Dictionary<string, double>> Days { get; set; } = [];
}

public sealed record AppUsage(string App, TimeSpan Time);

/// <summary>
/// Screen time per aplikasi: berapa lama tiap aplikasi ada di depan saat kamu sedang memakai laptop.
/// Hitungan berhenti saat layar dikunci atau kamu tidak di depan laptop (dilaporkan pemilik lewat
/// <see cref="Pause"/>). Hanya nama aplikasi dan lamanya yang disimpan (tanpa judul jendela), 14 hari
/// terakhir, di laptop sendiri.
/// </summary>
public sealed class ScreenTime
{
    public const int KeepDays = 14;

    private readonly ScreenTimeData _d;
    private string? _app;
    private DateTime _since;

    public ScreenTime(ScreenTimeData? restored = null)
    {
        _d = restored ?? new ScreenTimeData();
    }

    public ScreenTimeData Data => _d;

    public event Action? Changed;

    /// <summary>Aplikasi yang sedang dihitung (null saat jeda).</summary>
    public string? Current => _app;

    /// <summary>Aplikasi di depan berganti (atau mulai lagi setelah jeda).</summary>
    public void Switch(string? app, DateTime now)
    {
        Flush(now);
        _app = string.IsNullOrWhiteSpace(app) ? null : app;
        _since = now;
    }

    /// <summary>Berhenti menghitung sejak <paramref name="at"/> (layar dikunci, ditinggal).</summary>
    public void Pause(DateTime at)
    {
        Flush(at);
        _app = null;
    }

    /// <summary>Catat waktu yang sudah berjalan tanpa mengganti aplikasi (sebelum menyimpan atau menampilkan).</summary>
    public void Flush(DateTime now)
    {
        if (_app is null || now <= _since)
        {
            _since = now;
            return;
        }
        // Lewat tengah malam: pecah ke dua hari.
        var start = _since;
        while (start.Date < now.Date)
        {
            var midnight = start.Date.AddDays(1);
            Add(start.Date, _app, (midnight - start).TotalSeconds);
            start = midnight;
        }
        Add(now.Date, _app, (now - start).TotalSeconds);
        _since = now;
        Prune(now);
        Changed?.Invoke();
    }

    private void Add(DateTime day, string app, double seconds)
    {
        if (seconds <= 0) return;
        string key = Key(day);
        if (!_d.Days.TryGetValue(key, out var apps)) _d.Days[key] = apps = [];
        apps[app] = apps.GetValueOrDefault(app) + seconds;
    }

    private void Prune(DateTime now)
    {
        string oldest = Key(now.Date.AddDays(-(KeepDays - 1)));
        foreach (var key in _d.Days.Keys.Where(k => string.CompareOrdinal(k, oldest) < 0).ToList())
            _d.Days.Remove(key);
    }

    /// <summary>Aplikasi hari itu, dari yang paling lama.</summary>
    public IReadOnlyList<AppUsage> Day(DateTime day) =>
        _d.Days.TryGetValue(Key(day.Date), out var apps)
            ? apps.OrderByDescending(a => a.Value).Select(a => new AppUsage(a.Key, TimeSpan.FromSeconds(a.Value))).ToList()
            : [];

    public TimeSpan Total(DateTime day) =>
        _d.Days.TryGetValue(Key(day.Date), out var apps) ? TimeSpan.FromSeconds(apps.Values.Sum()) : TimeSpan.Zero;

    public void Clear()
    {
        _d.Days.Clear();
        Changed?.Invoke();
    }

    /// <summary>"2 j 5 m", "45 m", "< 1 m".</summary>
    public static string Format(TimeSpan t)
    {
        if (t.TotalMinutes < 1) return "< 1 m";
        int hours = (int)t.TotalHours;
        return hours > 0 ? $"{hours} j {t.Minutes} m" : $"{t.Minutes} m";
    }

    private static string Key(DateTime day) => day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
