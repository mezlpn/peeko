namespace Celah.Core.Notch;

/// <summary>
/// Isi pil saat collapsed. Urutan prioritas dari desain (Collapsed.dc.html):
/// pengingat minum → timer fokus → musik → idle. Untuk sekarang baru musik.
/// </summary>
public enum CollapsedActivity
{
    Idle,
    Music,
}
