using Peeko.Core.Productivity;

namespace Peeko.Core.Notch;

/// <summary>Isi pil saat collapsed.</summary>
public enum CollapsedActivity
{
    Idle,
    Music,
    Focus,
    /// <summary>Musik di kiri, timer di kanan (maksimal dua aktivitas).</summary>
    MusicAndFocus,
    /// <summary>Pengingat minum (sementara, ±8 dtk).</summary>
    Water,
}

/// <summary>
/// Memilih isi pil. Prioritas dari desain (Collapsed.dc.html): pengingat minum → timer fokus →
/// musik → idle; timer dan musik bisa tampil bersama.
/// </summary>
public static class ActivityArbiter
{
    public static CollapsedActivity Pick(bool musicLive, FocusStatus focus, bool waterReminder)
    {
        if (waterReminder) return CollapsedActivity.Water;
        bool timer = focus != FocusStatus.Ready;
        return (timer, musicLive) switch
        {
            (true, true) => CollapsedActivity.MusicAndFocus,
            (true, false) => CollapsedActivity.Focus,
            (false, true) => CollapsedActivity.Music,
            _ => CollapsedActivity.Idle,
        };
    }
}
