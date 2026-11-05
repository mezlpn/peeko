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
    /// <summary>Ada versi baru: menggantikan mata dan musik sampai dipasang.</summary>
    Update,
    /// <summary>Beberapa menit sebelum waktu salat: hitung mundur.</summary>
    Prayer,
    /// <summary>Tugas bertenggat: hitung mundur, lalu tombol centang/tunda saat waktunya.</summary>
    Task,
}

/// <summary>
/// Memilih isi pil. Prioritas dari desain (Collapsed.dc.html): pengingat minum → timer fokus →
/// musik → idle; timer dan musik bisa tampil bersama. Update tersedia menyela setelah timer:
/// mengganggu dengan sengaja, tapi tidak menyembunyikan hitung mundur orang yang sedang fokus.
/// Hitung mundur salat (hanya beberapa menit terakhir) menang atas semuanya kecuali pengingat minum,
/// lalu tugas bertenggat.
/// </summary>
public static class ActivityArbiter
{
    public static CollapsedActivity Pick(bool musicLive, FocusStatus focus, bool waterReminder, bool updateAvailable = false,
        bool prayerSoon = false, bool taskSoon = false)
    {
        if (waterReminder) return CollapsedActivity.Water;
        if (prayerSoon) return CollapsedActivity.Prayer;
        if (taskSoon) return CollapsedActivity.Task;
        bool timer = focus != FocusStatus.Ready;
        if (updateAvailable && !timer) return CollapsedActivity.Update;
        return (timer, musicLive) switch
        {
            (true, true) => CollapsedActivity.MusicAndFocus,
            (true, false) => CollapsedActivity.Focus,
            (false, true) => CollapsedActivity.Music,
            _ => CollapsedActivity.Idle,
        };
    }
}
