using System.Runtime.CompilerServices;
using Peeko.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Peeko.App.Settings;

/// <summary>Jembatan antara jendela Pengaturan dan <see cref="SettingsService"/>.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service;

    public SettingsViewModel(SettingsService service)
    {
        _service = service;
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

    // ── Modul ──
    public bool MusicEnabled { get => S.MusicEnabled; set => Set(value, s => s.MusicEnabled, (s, v) => s.MusicEnabled = v); }
    public bool LyricsEnabled { get => S.LyricsEnabled; set => Set(value, s => s.LyricsEnabled, (s, v) => s.LyricsEnabled = v); }
    public bool FocusEnabled { get => S.FocusEnabled; set => Set(value, s => s.FocusEnabled, (s, v) => s.FocusEnabled = v); }
    public bool WaterEnabled { get => S.WaterEnabled; set => Set(value, s => s.WaterEnabled, (s, v) => s.WaterEnabled = v); }
    public bool TodoEnabled { get => S.TodoEnabled; set => Set(value, s => s.TodoEnabled, (s, v) => s.TodoEnabled = v); }
    public bool ShelfEnabled { get => S.ShelfEnabled; set => Set(value, s => s.ShelfEnabled, (s, v) => s.ShelfEnabled = v); }
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
}
