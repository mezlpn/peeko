namespace Peeko.Core.Settings;

/// <summary>Kapan notch menjadi garis tipis (revisi #8).</summary>
public enum SlimMode
{
    /// <summary>Otomatis saat jendela aktif menempel ke tepi atas layar.</summary>
    Auto,
    /// <summary>Selalu berupa pil.</summary>
    AlwaysPill,
    /// <summary>Selalu berupa garis tipis.</summary>
    AlwaysLine,
}

/// <summary>Semua pengaturan pengguna, disimpan di settings.json. Perubahan langsung diterapkan.</summary>
public sealed class AppSettings
{
    // ── Umum ──
    public bool StartWithWindows { get; set; }
    public bool HideOnFullscreen { get; set; } = true;
    public SlimMode SlimMode { get; set; } = SlimMode.Auto;
    /// <summary>Geser horizontal dari tengah layar, dalam DIP (−300 … 300), mis. untuk menghindari webcam.</summary>
    public double HorizontalOffset { get; set; }

    // ── Modul ──
    public bool MusicEnabled { get; set; } = true;
    public bool LyricsEnabled { get; set; } = true;
    public bool FocusEnabled { get; set; } = true;
    public bool WaterEnabled { get; set; } = true;
    public bool TodoEnabled { get; set; } = true;
    public bool ShelfEnabled { get; set; } = true;
    public bool SystemPeeksEnabled { get; set; } = true;

    // ── Baterai ──
    public int BatteryThreshold { get; set; } = 20;

    // ── Minum ──
    public int WaterIntervalMinutes { get; set; } = 60;
    public int WaterGoal { get; set; } = 8;

    // ── Fokus ──
    public int FocusMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;

    // ── Lirik ──
    public bool FetchLyrics { get; set; } = true;

    public const int MinBattery = 10, MaxBattery = 50;
    public const double MaxOffset = 300;

    /// <summary>Jepit nilai dari file yang mungkin diedit manual.</summary>
    public AppSettings Normalize()
    {
        BatteryThreshold = Math.Clamp(BatteryThreshold, MinBattery, MaxBattery);
        WaterIntervalMinutes = Math.Clamp(WaterIntervalMinutes, 15, 240);
        WaterGoal = Math.Clamp(WaterGoal, 1, 20);
        FocusMinutes = Math.Clamp(FocusMinutes, 5, 120);
        ShortBreakMinutes = Math.Clamp(ShortBreakMinutes, 1, 30);
        LongBreakMinutes = Math.Clamp(LongBreakMinutes, 5, 60);
        HorizontalOffset = Math.Clamp(HorizontalOffset, -MaxOffset, MaxOffset);
        if (!Enum.IsDefined(SlimMode)) SlimMode = SlimMode.Auto;
        return this;
    }
}
