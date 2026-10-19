namespace Peeko.Core.Peeks;

/// <summary>Jenis event sesaat yang tampil sebagai peek (Peek.dc.html).</summary>
public enum PeekKind
{
    Volume,
    Brightness,
    CapsLock,
    Language,
    BatteryLow,
    BatteryCritical,
    Charging,
    Discharging,
    WifiConnected,
    WifiDisconnected,
    AudioDeviceConnected,
    AudioDeviceDisconnected,
    /// <summary>Sesi fokus selesai (Kedip senang).</summary>
    FocusDone,
    BreakDone,
    /// <summary>File diseret ke notch: "Simpan di rak".</summary>
    ShelfDrop,
    /// <summary>Versi baru ditemukan (Text = versi).</summary>
    UpdateAvailable,
    /// <summary>Baru saja diperbarui (Text = versi sekarang).</summary>
    Updated,
    /// <summary>Konversi selesai (Text = nama file hasil, Detail = keterangan).</summary>
    ConvertDone,
    /// <summary>Konversi gagal (Text = alasan singkat).</summary>
    ConvertFailed,
    /// <summary>Fitur Ubah format selesai dipasang (Text = versi Filee).</summary>
    ConvertReady,
}

/// <summary>Isi satu peek. Arti tiap field bergantung pada <see cref="Kind"/>.</summary>
public sealed record PeekRequest(PeekKind Kind)
{
    /// <summary>Level 0–1 (volume, kecerahan).</summary>
    public double Level { get; init; }

    /// <summary>Volume bisu / Caps Lock aktif / perangkat audio berupa headphone.</summary>
    public bool Flag { get; init; }

    /// <summary>Teks utama: SSID, nama perangkat, kode bahasa baru ("ID").</summary>
    public string? Text { get; init; }

    /// <summary>Teks pendukung: kode bahasa lama, nama bahasa, estimasi sisa baterai.</summary>
    public string? Detail { get; init; }

    /// <summary>Persentase baterai.</summary>
    public int? Percent { get; init; }

    /// <summary>Keterangan di kanan, mis. nama bahasa "Bahasa Indonesia".</summary>
    public string? Caption { get; init; }

    /// <summary>Lama tampil. Caps Lock mati lebih singkat; baterai lebih lama (Peek.dc.html).</summary>
    public TimeSpan Hold => Kind switch
    {
        PeekKind.CapsLock when !Flag => TimeSpan.FromSeconds(1),
        PeekKind.BatteryLow => TimeSpan.FromSeconds(3),
        PeekKind.BatteryCritical => TimeSpan.FromSeconds(5),
        PeekKind.FocusDone or PeekKind.BreakDone or PeekKind.UpdateAvailable or PeekKind.Updated => TimeSpan.FromSeconds(4),
        PeekKind.ConvertDone or PeekKind.ConvertReady => TimeSpan.FromSeconds(3),
        PeekKind.ConvertFailed => TimeSpan.FromSeconds(4),
        // Ditahan selama file masih diseret di atas notch; setelah tersimpan (Flag) hanya sebentar.
        PeekKind.ShelfDrop => Flag ? TimeSpan.FromMilliseconds(1500) : TimeSpan.FromSeconds(30),
        _ => TimeSpan.FromMilliseconds(1500),
    };

    /// <summary>Baterai kritis tetap tampil walau ada aplikasi fullscreen.</summary>
    public bool BypassHidden => Kind == PeekKind.BatteryCritical;

    /// <summary>Lebar pil peek (280–340 sesuai isi).</summary>
    public double Width => Kind switch
    {
        PeekKind.CapsLock => 280,
        PeekKind.ShelfDrop or PeekKind.ConvertDone or PeekKind.ConvertFailed => 340,
        PeekKind.Language or PeekKind.BatteryLow or PeekKind.Charging
            or PeekKind.WifiConnected or PeekKind.WifiDisconnected => 300,
        _ => 320,
    };
}
