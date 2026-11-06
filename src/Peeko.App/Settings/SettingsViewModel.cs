using System.Runtime.CompilerServices;
using Peeko.App.Conversion;
using Peeko.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Peeko.App.Settings;

/// <summary>Jembatan antara jendela Pengaturan dan <see cref="SettingsService"/>.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service;
    private readonly ConvertController _convert;
    private readonly Today.WeatherService _weather;
    private readonly Prayer.PrayerController _prayer;

    public SettingsViewModel(SettingsService service, ConvertController convert, Today.WeatherService weather,
        Prayer.PrayerController prayer, Today.CalendarService calendar)
    {
        _calendar = calendar;
        _calendarInput = string.Join(Environment.NewLine, service.Current.CalendarUrls);
        calendar.Updated += () => OnPropertyChanged(nameof(CalendarStatus));
        _service = service;
        _convert = convert;
        _weather = weather;
        _prayer = prayer;
        _cityInput = service.Current.WeatherCity ?? "";
        convert.Changed += () =>
        {
            OnPropertyChanged(nameof(FileeStatus));
            OnPropertyChanged(nameof(FileeDetail));
            OnPropertyChanged(nameof(FileeButtonText));
            OnPropertyChanged(nameof(FileeButtonEnabled));
            OnPropertyChanged(nameof(EnginesEnabled));
        };
    }

    private AppSettings S => _service.Current;

    private void Set<T>(T value, Func<AppSettings, T> get, Action<AppSettings, T> set, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(get(S), value)) return;
        _service.Update(s => set(s, value));
        OnPropertyChanged(name);
    }

    public string Version => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    // ── Umum ──
    public bool StartWithWindows { get => S.StartWithWindows; set => Set(value, s => s.StartWithWindows, (s, v) => s.StartWithWindows = v); }
    public bool HideOnFullscreen { get => S.HideOnFullscreen; set => Set(value, s => s.HideOnFullscreen, (s, v) => s.HideOnFullscreen = v); }
    public double HorizontalOffset { get => S.HorizontalOffset; set => Set(Math.Round(value), s => s.HorizontalOffset, (s, v) => s.HorizontalOffset = v); }

    public SlimMode SlimMode
    {
        get => S.SlimMode;
        set
        {
            Set(value, s => s.SlimMode, (s, v) => s.SlimMode = v);
            OnPropertyChanged(nameof(SlimModeText));
        }
    }

    public string SlimModeText => SlimMode switch
    {
        SlimMode.AlwaysPill => "Selalu pil",
        SlimMode.AlwaysLine => "Selalu garis",
        _ => "Otomatis",
    };

    public Core.Notch.CollapsedSlot CollapsedLeft
    {
        get => S.CollapsedLeft;
        set
        {
            Set(value, s => s.CollapsedLeft, (s, v) => s.CollapsedLeft = v);
            OnPropertyChanged(nameof(CollapsedLeftText));
        }
    }

    public Core.Notch.CollapsedSlot CollapsedRight
    {
        get => S.CollapsedRight;
        set
        {
            Set(value, s => s.CollapsedRight, (s, v) => s.CollapsedRight = v);
            OnPropertyChanged(nameof(CollapsedRightText));
        }
    }

    public string CollapsedLeftText => Core.Notch.CollapsedSlots.Label(CollapsedLeft);
    public string CollapsedRightText => Core.Notch.CollapsedSlots.Label(CollapsedRight);

    // ── Modul ──
    public bool MusicEnabled { get => S.MusicEnabled; set => Set(value, s => s.MusicEnabled, (s, v) => s.MusicEnabled = v); }
    public bool LyricsEnabled { get => S.LyricsEnabled; set => Set(value, s => s.LyricsEnabled, (s, v) => s.LyricsEnabled = v); }
    public bool FocusEnabled { get => S.FocusEnabled; set => Set(value, s => s.FocusEnabled, (s, v) => s.FocusEnabled = v); }
    public bool WaterEnabled { get => S.WaterEnabled; set => Set(value, s => s.WaterEnabled, (s, v) => s.WaterEnabled = v); }
    public bool TodoEnabled { get => S.TodoEnabled; set => Set(value, s => s.TodoEnabled, (s, v) => s.TodoEnabled = v); }
    public bool ShelfEnabled { get => S.ShelfEnabled; set => Set(value, s => s.ShelfEnabled, (s, v) => s.ShelfEnabled = v); }
    public bool ControlEnabled { get => S.ControlEnabled; set => Set(value, s => s.ControlEnabled, (s, v) => s.ControlEnabled = v); }
    public bool TodayEnabled { get => S.TodayEnabled; set => Set(value, s => s.TodayEnabled, (s, v) => s.TodayEnabled = v); }
    public bool SystemPeeksEnabled { get => S.SystemPeeksEnabled; set => Set(value, s => s.SystemPeeksEnabled, (s, v) => s.SystemPeeksEnabled = v); }

    // ── Baterai ──
    public double BatteryThreshold { get => S.BatteryThreshold; set => Set((int)Math.Round(value / 5) * 5, s => s.BatteryThreshold, (s, v) => s.BatteryThreshold = v); }

    // ── Minum ──
    public int WaterIntervalMinutes { get => S.WaterIntervalMinutes; set => Set(value, s => s.WaterIntervalMinutes, (s, v) => s.WaterIntervalMinutes = v); }
    public int WaterGoal { get => S.WaterGoal; set => Set(value, s => s.WaterGoal, (s, v) => s.WaterGoal = v); }

    // ── Fokus ──
    public int FocusMinutes { get => S.FocusMinutes; set => Set(value, s => s.FocusMinutes, (s, v) => s.FocusMinutes = v); }
    public int ShortBreakMinutes { get => S.ShortBreakMinutes; set => Set(value, s => s.ShortBreakMinutes, (s, v) => s.ShortBreakMinutes = v); }
    public int LongBreakMinutes { get => S.LongBreakMinutes; set => Set(value, s => s.LongBreakMinutes, (s, v) => s.LongBreakMinutes = v); }

    // ── Lirik ──
    public bool FetchLyrics { get => S.FetchLyrics; set => Set(value, s => s.FetchLyrics, (s, v) => s.FetchLyrics = v); }
    public bool RomanizeLyrics { get => S.RomanizeLyrics; set => Set(value, s => s.RomanizeLyrics, (s, v) => s.RomanizeLyrics = v); }

    // ── Cuaca ──
    public bool WeatherEnabled { get => S.WeatherEnabled; set => Set(value, s => s.WeatherEnabled, (s, v) => s.WeatherEnabled = v); }

    private string _cityInput;
    public string CityInput { get => _cityInput; set => SetProperty(ref _cityInput, value); }

    // ── Kalender ──
    private readonly Today.CalendarService _calendar;
    private string _calendarInput;

    public string CalendarUrlsInput { get => _calendarInput; set => SetProperty(ref _calendarInput, value); }

    public string CalendarStatus => !_calendar.IsEnabled ? "Belum ada kalender"
        : _calendar.LastError ?? $"{_calendar.Events.Count} acara dalam seminggu ini";

    public string GitHubUser { get => S.GitHubUser ?? ""; set => Set(string.IsNullOrWhiteSpace(value) ? null : value.Trim(), s => s.GitHubUser, (s, v) => s.GitHubUser = v); }
    public string GitHubToken { get => S.GitHubToken ?? ""; set => Set(string.IsNullOrWhiteSpace(value) ? null : value.Trim(), s => s.GitHubToken, (s, v) => s.GitHubToken = v); }

    public void SaveCalendar()
    {
        var urls = CalendarUrlsInput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(u => u.StartsWith("http", StringComparison.OrdinalIgnoreCase) || u.StartsWith("webcal", StringComparison.OrdinalIgnoreCase))
            .Distinct().ToList();
        _service.Update(s => s.CalendarUrls = urls);
        CalendarUrlsInput = string.Join(Environment.NewLine, urls);
        OnPropertyChanged(nameof(CalendarStatus));
    }

    private string _cityStatus = "";
    public string CityStatus { get => _cityStatus; private set => SetProperty(ref _cityStatus, value); }

    public string WeatherLocationText => S.WeatherPlace is { Length: > 0 } place
        ? $"Sekarang: {place}" + (string.IsNullOrWhiteSpace(S.WeatherCity) ? " (perkiraan dari IP)" : "")
        : "Kosongkan untuk memperkirakan lokasi dari alamat IP";

    /// <summary>Simpan kota; kosong = kembali ke perkiraan IP.</summary>
    public async Task SaveCityAsync()
    {
        CityStatus = "Mencari…";
        var found = await _weather.SetCityAsync(CityInput);
        CityStatus = found switch
        {
            null => "Kota tidak ditemukan atau tidak ada internet",
            "" => "Lokasi akan diperkirakan dari alamat IP",
            _ => $"Tersimpan: {found}",
        };
        OnPropertyChanged(nameof(WeatherLocationText));
    }

    // ── Catatan & kesehatan ──
    public bool NotesEnabled { get => S.NotesEnabled; set => Set(value, s => s.NotesEnabled, (s, v) => s.NotesEnabled = v); }
    public bool HealthEnabled { get => S.HealthEnabled; set => Set(value, s => s.HealthEnabled, (s, v) => s.HealthEnabled = v); }
    public bool SystemEnabled { get => S.SystemEnabled; set => Set(value, s => s.SystemEnabled, (s, v) => s.SystemEnabled = v); }
    public bool ScreenTimeEnabled { get => S.ScreenTimeEnabled; set => Set(value, s => s.ScreenTimeEnabled, (s, v) => s.ScreenTimeEnabled = v); }
    public bool LauncherEnabled { get => S.LauncherEnabled; set => Set(value, s => s.LauncherEnabled, (s, v) => s.LauncherEnabled = v); }
    public bool ClipboardEnabled { get => S.ClipboardEnabled; set => Set(value, s => s.ClipboardEnabled, (s, v) => s.ClipboardEnabled = v); }
    public bool ClaudeCodeEnabled
    {
        get => S.ClaudeCodeEnabled;
        set
        {
            Set(value, s => s.ClaudeCodeEnabled, (s, v) => s.ClaudeCodeEnabled = v);
            OnPropertyChanged(nameof(ClaudeStatus));
        }
    }

    public string ClaudeStatus => !S.ClaudeCodeEnabled ? "Mati"
        : Sistem.ClaudeCodeService.SettingsPath is { } path && System.IO.File.Exists(path) && System.IO.File.ReadAllText(path).Contains("127.0.0.1:47821/claude")
            ? "Hook terpasang. Kabar dan izin Claude Code muncul di notch."
            : "Hook belum dipasang. Klik Pasang hook.";

    public void RefreshClaudeStatus() => OnPropertyChanged(nameof(ClaudeStatus));

    public bool RadioEnabled { get => S.RadioEnabled; set => Set(value, s => s.RadioEnabled, (s, v) => s.RadioEnabled = v); }
    public bool SnapZonesEnabled { get => S.SnapZonesEnabled; set => Set(value, s => s.SnapZonesEnabled, (s, v) => s.SnapZonesEnabled = v); }
    public bool BreaksEnabled { get => S.BreaksEnabled; set => Set(value, s => s.BreaksEnabled, (s, v) => s.BreaksEnabled = v); }
    public int BreakIntervalMinutes { get => S.BreakIntervalMinutes; set => Set(value, s => s.BreakIntervalMinutes, (s, v) => s.BreakIntervalMinutes = v); }
    public bool EyeBreaksEnabled { get => S.EyeBreaksEnabled; set => Set(value, s => s.EyeBreaksEnabled, (s, v) => s.EyeBreaksEnabled = v); }

    // ── Tugas ──
    public bool TaskRemindersEnabled { get => S.TaskRemindersEnabled; set => Set(value, s => s.TaskRemindersEnabled, (s, v) => s.TaskRemindersEnabled = v); }
    public int TaskCountdownMinutes { get => S.TaskCountdownMinutes; set => Set(value, s => s.TaskCountdownMinutes, (s, v) => s.TaskCountdownMinutes = v); }

    // ── Jadwal salat ──
    public bool PrayerEnabled { get => S.PrayerEnabled; set => Set(value, s => s.PrayerEnabled, (s, v) => s.PrayerEnabled = v); }
    public int PrayerReminderMinutes { get => S.PrayerReminderMinutes; set => Set(value, s => s.PrayerReminderMinutes, (s, v) => s.PrayerReminderMinutes = v); }
    public int PrayerCountdownMinutes { get => S.PrayerCountdownMinutes; set => Set(value, s => s.PrayerCountdownMinutes, (s, v) => s.PrayerCountdownMinutes = v); }
    public int PrayerOffsetMinutes { get => S.PrayerOffsetMinutes; set => Set(value, s => s.PrayerOffsetMinutes, (s, v) => s.PrayerOffsetMinutes = v); }
    public bool AdhanEnabled { get => S.AdhanEnabled; set => Set(value, s => s.AdhanEnabled, (s, v) => s.AdhanEnabled = v); }

    public string AdhanFileText => S.AdhanPath is { Length: > 0 } path ? System.IO.Path.GetFileName(path) : "Belum ada file dipilih";

    public bool HasAdhanFile => S.AdhanPath is { Length: > 0 } path && System.IO.File.Exists(path);

    public string PrayerLocationText => S.WeatherPlace is { Length: > 0 } place
        ? $"Dihitung untuk {place} (lokasi yang sama dengan cuaca)"
        : "Lokasi diambil dari pengaturan Cuaca";

    public void SetAdhanFile(string path)
    {
        _service.Update(s =>
        {
            s.AdhanPath = path;
            s.AdhanEnabled = true;
        });
        OnPropertyChanged(nameof(AdhanFileText));
        OnPropertyChanged(nameof(HasAdhanFile));
        OnPropertyChanged(nameof(AdhanEnabled));
    }

    public void ToggleAdhanPreview()
    {
        if (_prayer.AdhanPlaying) _prayer.StopAdhan();
        else if (S.AdhanPath is { } path) _prayer.PreviewAdhan(path);
    }

    // ── Ubah format (Filee) ──
    public bool ConvertEnabled { get => S.ConvertEnabled; set => Set(value, s => s.ConvertEnabled, (s, v) => s.ConvertEnabled = v); }

    private FileeService Filee => _convert.Filee;

    public string FileeStatus => _convert.Installing
        ? (_convert.InstallProgress >= 1 ? "Memasang Filee…" : $"Mengunduh Filee · {Math.Round(_convert.InstallProgress * 100)}%")
        : Filee.IsReady ? $"Filee {Filee.Version} terpasang" : "Filee belum dipasang";

    public string FileeDetail => _convert.Message is { } error && !_convert.Installing && _convert.Stage == ConvertStage.Idle
        ? error
        : Filee.IsReady
            ? Filee.IsOwnCopy ? "Disimpan di folder data Peeko" : "Memakai Filee yang sudah ada di laptop ini"
            : "Unduh ±160 MB sekali saja dari rilis resmi Filee";

    /// <summary>Pasang / Hapus. Filee yang dipasang sendiri oleh pengguna tidak dihapus Peeko.</summary>
    public string FileeButtonText => Filee.IsReady ? "Hapus" : "Pasang";

    public bool FileeButtonEnabled => !_convert.Installing && (!Filee.IsReady || Filee.IsOwnCopy);

    public bool EnginesEnabled => Filee.AppPath is not null;

    public void ToggleFilee()
    {
        if (Filee.IsReady) _ = _convert.UninstallAsync();
        else _ = _convert.InstallAsync();
    }

    public void OpenEngines() => _convert.OpenEngines();
}
