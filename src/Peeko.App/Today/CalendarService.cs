using System.Net.Http;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Peeko.App.Settings;
using Peeko.Core.Peeks;
using Peeko.Core.Status;

namespace Peeko.App.Today;

/// <summary>
/// Kalender dari tautan iCal (.ics) — Google Calendar ("alamat rahasia dalam format iCal"), Outlook
/// ("terbitkan kalender"), atau layanan lain. Mati selama belum ada tautan. Diambil saat mulai, lalu
/// tiap 30 menit dan saat tab Hari ini dibuka (paling sering tiap 5 menit). Pengingat rapat memakai
/// satu timer sekali-jalan menuju acara berikutnya.
/// </summary>
public sealed class CalendarService : IDisposable
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MinRefresh = TimeSpan.FromMinutes(5);

    private readonly SettingsService _settings;
    private readonly ILogger<CalendarService> _log;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly DispatcherTimer _refresh = new(DispatcherPriority.Background) { Interval = RefreshEvery };
    private readonly DispatcherTimer _remind = new(DispatcherPriority.Background);
    private readonly HashSet<string> _reminded = [];
    private DateTime _lastFetch = DateTime.MinValue;
    private string _urlsKey = "";
    private bool _fetching;

    public CalendarService(SettingsService settings, ILogger<CalendarService> log)
    {
        _settings = settings;
        _log = log;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Peeko");
        _refresh.Tick += async (_, _) => await FetchAsync();
        _remind.Tick += (_, _) => Remind();
    }

    /// <summary>Acara dari kemarin sampai 8 hari ke depan.</summary>
    public IReadOnlyList<CalendarEvent> Events { get; private set; } = [];

    public bool IsEnabled => Urls.Count > 0;

    /// <summary>Pesan galat terakhir (untuk Pengaturan), null jika semua berhasil.</summary>
    public string? LastError { get; private set; }

    public event Action? Updated;

    public event Action<PeekRequest>? PeekRequested;

    private IReadOnlyList<string> Urls => _settings.Current.CalendarUrls;

    public void Apply()
    {
        var key = string.Join('\n', Urls);
        if (key == _urlsKey) return;
        _urlsKey = key;
        if (!IsEnabled)
        {
            _refresh.Stop();
            _remind.Stop();
            Events = [];
            LastError = null;
            Updated?.Invoke();
            return;
        }
        _refresh.Start();
        _ = FetchAsync();
    }

    /// <summary>Tab Hari ini dibuka: perbarui kalau sudah agak lama.</summary>
    public void RefreshIfStale()
    {
        if (IsEnabled && DateTime.Now - _lastFetch > MinRefresh) _ = FetchAsync();
    }

    public IReadOnlyList<CalendarEvent> Today(DateTime now) =>
        [.. Events.Where(e => e.End > now && e.Start < now.Date.AddDays(1))];

    public CalendarEvent? Next(DateTime now) => Events.FirstOrDefault(e => !e.AllDay && e.Start > now);

    public async Task FetchAsync()
    {
        if (_fetching || !IsEnabled) return;
        _fetching = true;
        try
        {
            var now = DateTime.Now;
            var from = now.Date.AddDays(-1);
            var to = now.Date.AddDays(8);
            var all = new List<CalendarEvent>();
            string? error = null;
            foreach (var raw in Urls)
            {
                var url = raw.Trim();
                if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)) url = "https://" + url[9..];
                try
                {
                    var ics = await _http.GetStringAsync(url);
                    all.AddRange(await Task.Run(() => ICalendar.Parse(ics, from, to)));
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException or InvalidOperationException)
                {
                    error = ex is HttpRequestException { StatusCode: { } code } ? $"Tautan kalender ditolak ({(int)code})" : "Kalender belum bisa dimuat";
                    _log.LogInformation("Kalender gagal dimuat: {Message}", ex.Message);
                }
            }
            LastError = error;
            _lastFetch = DateTime.Now;
            // Gagal total: pertahankan daftar lama (mungkin sedang offline).
            if (all.Count > 0 || error is null) Events = all.OrderBy(e => e.Start).ToList();
            Schedule();
            Updated?.Invoke();
        }
        finally
        {
            _fetching = false;
        }
    }

    private TimeSpan Lead => TimeSpan.FromMinutes(_settings.Current.CalendarReminderMinutes);

    private void Schedule()
    {
        _remind.Stop();
        var now = DateTime.Now;
        var next = Events.FirstOrDefault(e => !e.AllDay && e.Start > now && !_reminded.Contains(Key(e)));
        if (next is null) return;
        var due = next.Start - Lead - now;
        _remind.Interval = due < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : due;
        _remind.Start();
    }

    private void Remind()
    {
        _remind.Stop();
        var now = DateTime.Now;
        foreach (var e in Events.Where(e => !e.AllDay && e.Start > now - TimeSpan.FromMinutes(1) && e.Start - Lead <= now + TimeSpan.FromSeconds(5)))
        {
            if (!_reminded.Add(Key(e))) continue;
            int minutes = (int)Math.Round((e.Start - now).TotalMinutes);
            PeekRequested?.Invoke(new PeekRequest(PeekKind.Meeting)
            {
                Text = e.Title,
                Detail = minutes <= 0 ? $"mulai · {e.Start:HH.mm}" : $"{minutes} menit lagi · {e.Start:HH.mm}",
                Flag = e.JoinUrl is not null,
            });
        }
        Schedule();
    }

    private static string Key(CalendarEvent e) => $"{e.Start:O}|{e.Title}";

    public void Dispose()
    {
        _refresh.Stop();
        _remind.Stop();
        _http.Dispose();
    }
}
