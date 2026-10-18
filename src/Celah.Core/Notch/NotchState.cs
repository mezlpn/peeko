namespace Celah.Core.Notch;

public enum NotchState
{
    /// <summary>Notch menyingkir ke atas layar (aplikasi fullscreen atau disembunyikan pengguna).</summary>
    Hidden,

    /// <summary>Pil kecil, state default.</summary>
    Collapsed,

    /// <summary>Pil melebar sesaat untuk event singkat (volume, baterai, dsb.).</summary>
    Peek,

    /// <summary>Panel penuh dengan tab modul.</summary>
    Expanded,
}

/// <summary>Durasi perilaku notch. Nilai default diambil dari token desain (Tokens.dc.html).</summary>
public sealed record NotchTimings(
    TimeSpan HoverOpenDelay,
    TimeSpan LeaveCloseDelay,
    TimeSpan PeekHold,
    TimeSpan FullscreenSettle)
{
    public static NotchTimings Default { get; } = new(
        HoverOpenDelay: TimeSpan.FromMilliseconds(120),
        LeaveCloseDelay: TimeSpan.FromMilliseconds(350),
        PeekHold: TimeSpan.FromMilliseconds(1500),
        FullscreenSettle: TimeSpan.FromMilliseconds(400));
}
