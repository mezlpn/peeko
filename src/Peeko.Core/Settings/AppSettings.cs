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
    /// <summary>Info di kiri dan kanan notch tertutup.</summary>
    public Notch.CollapsedSlot CollapsedLeft { get; set; } = Notch.CollapsedSlot.Clock;
    public Notch.CollapsedSlot CollapsedRight { get; set; } = Notch.CollapsedSlot.Battery;

    // ── Tampilan ──
    /// <summary>Warna aksen (#RRGGBB); kosong = limau bawaan.</summary>
    public string? AccentColor { get; set; }
    public NotchStyle NotchStyle { get; set; } = NotchStyle.Black;

    // ── Modul ──
    public bool MusicEnabled { get; set; } = true;
    public bool LyricsEnabled { get; set; } = true;
    /// <summary>Cahaya berwarna sampul di belakang cover di tab Musik.</summary>
    public bool CoverGlowEnabled { get; set; } = true;
    /// <summary>Pin lirik (issue #1): pil musik saat notch mengecil menampilkan baris lirik yang sedang diputar.</summary>
    public bool LyricsPinned { get; set; }
    public bool FocusEnabled { get; set; } = true;
    public bool WaterEnabled { get; set; } = true;
    public bool TodoEnabled { get; set; } = true;
    public bool ShelfEnabled { get; set; } = true;
    /// <summary>Command Center: Wi-Fi, Bluetooth, jangan ganggu, volume, kecerahan.</summary>
    public bool ControlEnabled { get; set; } = true;
    /// <summary>Tab Hari ini: kalender, cuaca, baterai.</summary>
    public bool TodayEnabled { get; set; } = true;
    /// <summary>Zona "Ubah format" saat file diseret ke notch (memakai Filee).</summary>
    public bool ConvertEnabled { get; set; } = true;
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
    /// <summary>Tulis lirik Hangul/kana dengan huruf Latin.</summary>
    public bool RomanizeLyrics { get; set; }

    // ── Cuaca (tab Hari ini) ──
    public bool WeatherEnabled { get; set; } = true;
    /// <summary>Kota pilihan pengguna; kosong = perkiraan dari alamat IP.</summary>
    public string? WeatherCity { get; set; }
    /// <summary>Koordinat tersimpan untuk kota/perkiraan IP, supaya lokasi tidak dicari ulang tiap kali.</summary>
    public double? WeatherLatitude { get; set; }
    public double? WeatherLongitude { get; set; }
    public string? WeatherPlace { get; set; }

    // ── Catatan & kesehatan ──
    public bool NotesEnabled { get; set; } = true;
    /// <summary>Kartu istirahat gerak, mata, dan latihan napas di tab Sehat.</summary>
    public bool HealthEnabled { get; set; } = true;
    public bool BreaksEnabled { get; set; }
    public int BreakIntervalMinutes { get; set; } = 50;
    public bool EyeBreaksEnabled { get; set; }

    // ── Tugas ──
    /// <summary>Pengingat dan hitung mundur untuk tugas yang punya jam tenggat.</summary>
    public bool TaskRemindersEnabled { get; set; } = true;
    /// <summary>Pil hitung mundur muncul sekian menit sebelum tenggat; 0 = hanya saat tenggat.</summary>
    public int TaskCountdownMinutes { get; set; } = 15;
    /// <summary>Konfeti saat semua tugas selesai dicentang.</summary>
    public bool ConfettiEnabled { get; set; } = true;

    // ── Jadwal salat ──
    public bool PrayerEnabled { get; set; } = true;
    /// <summary>Peek pengingat sekian menit sebelum waktu salat; 0 = tanpa pengingat awal.</summary>
    public int PrayerReminderMinutes { get; set; } = 10;
    /// <summary>Pil hitung mundur muncul sekian menit sebelum waktu salat; 0 = tidak pernah.</summary>
    public int PrayerCountdownMinutes { get; set; } = 10;
    /// <summary>Putar azan dari file pilihan pengguna saat masuk waktu salat.</summary>
    public bool AdhanEnabled { get; set; }
    public string? AdhanPath { get; set; }
    /// <summary>Koreksi manual tiap waktu (menit), mis. menyamakan dengan jadwal masjid setempat.</summary>
    public int PrayerOffsetMinutes { get; set; }

    // ── Sistem & aplikasi ──
    /// <summary>Tab Sistem: CPU/RAM/GPU, progres hari, screen time.</summary>
    public bool SystemEnabled { get; set; } = true;
    /// <summary>Hitung lama tiap aplikasi di depan. Mati dari awal (privasi).</summary>
    public bool ScreenTimeEnabled { get; set; }
    /// <summary>Tab Aplikasi: peluncur aplikasi favorit.</summary>
    public bool LauncherEnabled { get; set; } = true;
    /// <summary>Riwayat clipboard (tab Clipboard). Mati dari awal (privasi).</summary>
    public bool ClipboardEnabled { get; set; }
    /// <summary>Seret jendela ke notch untuk menatanya. Mati dari awal.</summary>
    public bool SnapZonesEnabled { get; set; }

    // ── Kalender ──
    /// <summary>Tautan iCal (.ics) rahasia dari Google/Outlook. Kosong = kalender mati.</summary>
    public List<string> CalendarUrls { get; set; } = [];
    /// <summary>Peek pengingat sekian menit sebelum acara.</summary>
    public int CalendarReminderMinutes { get; set; } = 5;

    // ── Radio ──
    /// <summary>Tab Radio (Radio Browser, butuh internet). Mati dari awal.</summary>
    public bool RadioEnabled { get; set; }
    public List<Media.RadioStation> RadioFavorites { get; set; } = [];

    // ── GitHub ──
    /// <summary>Username untuk pull request yang menunggu review. Kosong = mati.</summary>
    public string? GitHubUser { get; set; }
    /// <summary>Token opsional (repo privat), diisi sendiri oleh pengguna.</summary>
    public string? GitHubToken { get; set; }

    // ── Claude Code ──
    /// <summary>Kabar dan permintaan izin Claude Code di notch. Mati dari awal.</summary>
    public bool ClaudeCodeEnabled { get; set; }
    /// <summary>Token acak yang dipasang di perintah hook supaya hanya hook Peeko yang diterima.</summary>
    public string? ClaudeToken { get; set; }

    // ── AI ──
    /// <summary>Tab AI. Mati dari awal.</summary>
    public bool AiEnabled { get; set; }
    /// <summary>"anthropic", "gemini", atau "openai" (API kompatibel OpenAI, termasuk Ollama).</summary>
    public string AiProvider { get; set; } = "anthropic";
    public string? AiKey { get; set; }
    public string? AiModel { get; set; }
    /// <summary>Alamat API kompatibel OpenAI, mis. https://api.openai.com/v1 atau http://localhost:11434/v1.</summary>
    public string? AiBaseUrl { get; set; }

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
        PrayerReminderMinutes = Math.Clamp(PrayerReminderMinutes, 0, 60);
        TaskCountdownMinutes = Math.Clamp(TaskCountdownMinutes, 0, 60);
        BreakIntervalMinutes = Math.Clamp(BreakIntervalMinutes, 20, 120);
        PrayerCountdownMinutes = Math.Clamp(PrayerCountdownMinutes, 0, 60);
        PrayerOffsetMinutes = Math.Clamp(PrayerOffsetMinutes, -10, 10);
        if (!Enum.IsDefined(SlimMode)) SlimMode = SlimMode.Auto;
        if (!Enum.IsDefined(NotchStyle)) NotchStyle = NotchStyle.Black;
        if (!Enum.IsDefined(CollapsedLeft)) CollapsedLeft = Notch.CollapsedSlot.None;
        if (!Enum.IsDefined(CollapsedRight)) CollapsedRight = Notch.CollapsedSlot.None;
        return this;
    }
}
