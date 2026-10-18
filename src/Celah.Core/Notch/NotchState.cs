namespace Celah.Core.Notch;

public enum NotchState
{
    /// <summary>Notch menyingkir ke atas layar (aplikasi fullscreen atau disembunyikan pengguna).</summary>
    Hidden,

    /// <summary>
    /// Garis tipis di tepi atas. Dipakai saat jendela aktif menempel ke tepi atas layar
    /// (mis. Chrome yang di-maximize) supaya tab-nya tidak tertutup.
    /// </summary>
    Slim,

    /// <summary>Pil kecil, state default.</summary>
    Collapsed,

    /// <summary>Pil melebar sesaat untuk event singkat (volume, baterai, dsb.).</summary>
    Peek,

    /// <summary>Panel penuh dengan tab modul.</summary>
    Expanded,
}

/// <summary>Durasi perilaku notch. Nilai awal dari token desain, dengan revisi hover (lihat ARSITEKTUR §10).</summary>
public sealed record NotchTimings(
    TimeSpan HoverOpenDelay,
    TimeSpan SlimRevealDelay,
    TimeSpan LeaveCloseDelay,
    TimeSpan PeekHold,
    TimeSpan FullscreenSettle)
{
    public static NotchTimings Default { get; } = new(
        HoverOpenDelay: TimeSpan.FromMilliseconds(60),
        SlimRevealDelay: TimeSpan.FromMilliseconds(150),
        LeaveCloseDelay: TimeSpan.FromMilliseconds(350),
        PeekHold: TimeSpan.FromMilliseconds(1500),
        FullscreenSettle: TimeSpan.FromMilliseconds(400));
}
