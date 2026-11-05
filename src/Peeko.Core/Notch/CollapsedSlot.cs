namespace Peeko.Core.Notch;

/// <summary>Info kecil di kiri/kanan notch yang tertutup (di samping mata atau pil musik).</summary>
public enum CollapsedSlot
{
    None,
    Clock,
    Date,
    Weather,
    Battery,
    NextPrayer,
    NextTask,
}

public static class CollapsedSlots
{
    public static string Label(CollapsedSlot slot) => slot switch
    {
        CollapsedSlot.Clock => "Jam",
        CollapsedSlot.Date => "Tanggal",
        CollapsedSlot.Weather => "Cuaca",
        CollapsedSlot.Battery => "Baterai",
        CollapsedSlot.NextPrayer => "Salat berikutnya",
        CollapsedSlot.NextTask => "Tugas berikutnya",
        _ => "Kosong",
    };

    /// <summary>Isi yang berubah tiap menit (perlu tick per menit selama notch tertutup terlihat).</summary>
    public static bool IsTimeBased(CollapsedSlot slot) =>
        slot is CollapsedSlot.Clock or CollapsedSlot.Date or CollapsedSlot.Battery or CollapsedSlot.NextPrayer or CollapsedSlot.NextTask;
}
