namespace Peeko.Core.Productivity;

public sealed class ClipItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public bool Pinned { get; set; }
    public DateTimeOffset At { get; set; }
}

public sealed class ClipboardData
{
    /// <summary>Hanya item yang di-pin yang disimpan ke disk.</summary>
    public List<ClipItem> Pinned { get; set; } = [];
}

/// <summary>
/// Riwayat teks yang disalin. Yang di-pin di atas dan tersimpan; sisanya hanya di memori (hilang saat
/// Peeko keluar) dan dibatasi <see cref="MaxRecent"/>. Menyalin teks yang sama memindahkannya ke atas.
/// </summary>
public sealed class ClipboardHistory
{
    public const int MaxRecent = 30;
    public const int MaxLength = 100_000;

    private readonly TimeProvider _time;
    private readonly List<ClipItem> _items = [];

    public ClipboardHistory(TimeProvider time, ClipboardData? restored = null)
    {
        _time = time;
        if (restored is not null) _items.AddRange(restored.Pinned.Where(p => !string.IsNullOrEmpty(p.Text)).Select(p => { p.Pinned = true; return p; }));
    }

    public event Action? Changed;

    /// <summary>Di-pin dulu (urutan pin), lalu yang terbaru.</summary>
    public IReadOnlyList<ClipItem> Items =>
        [.. _items.Where(i => i.Pinned), .. _items.Where(i => !i.Pinned).OrderByDescending(i => i.At)];

    public ClipboardData Data => new() { Pinned = [.. _items.Where(i => i.Pinned)] };

    public void Add(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > MaxLength) text = text[..MaxLength];
        var existing = _items.FirstOrDefault(i => i.Text == text);
        if (existing is not null)
        {
            existing.At = _time.GetUtcNow();
            Changed?.Invoke();
            return;
        }
        _items.Add(new ClipItem { Text = text, At = _time.GetUtcNow() });
        var recent = _items.Where(i => !i.Pinned).OrderByDescending(i => i.At).Skip(MaxRecent).ToList();
        foreach (var old in recent) _items.Remove(old);
        Changed?.Invoke();
    }

    public void TogglePin(Guid id)
    {
        if (_items.FirstOrDefault(i => i.Id == id) is not { } item) return;
        item.Pinned = !item.Pinned;
        // Dipin dipindah ke akhir daftar pin; dilepas kembali ke posisi menurut waktunya.
        _items.Remove(item);
        _items.Add(item);
        Changed?.Invoke();
    }

    public void Remove(Guid id)
    {
        if (_items.RemoveAll(i => i.Id == id) > 0) Changed?.Invoke();
    }

    /// <summary>Hapus semua yang tidak di-pin.</summary>
    public void Clear()
    {
        if (_items.RemoveAll(i => !i.Pinned) > 0) Changed?.Invoke();
    }

    /// <summary>Satu baris untuk daftar: spasi/baris baru dirapikan.</summary>
    public static string Preview(string text)
    {
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length > 160 ? line[..160] + "…" : line;
    }
}
