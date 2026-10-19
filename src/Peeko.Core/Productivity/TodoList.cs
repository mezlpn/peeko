namespace Peeko.Core.Productivity;

public sealed class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public bool Done { get; set; }
    public DateTimeOffset? DoneAt { get; set; }
}

public sealed class TodoData
{
    public List<TodoItem> Items { get; set; } = [];
}

/// <summary>
/// Daftar tugas singkat. Tugas belum selesai di atas, yang selesai di bawah; tugas selesai
/// dibersihkan otomatis keesokan harinya (WaterTodo.dc.html).
/// </summary>
public sealed class TodoList
{
    public const int MaxLength = 200;

    private readonly TimeProvider _time;
    private readonly TodoData _d;

    public TodoList(TimeProvider time, TodoData? restored = null)
    {
        _time = time;
        _d = restored ?? new TodoData();
        CleanUpOldDone();
    }

    public event Action? Changed;

    public TodoData Data => _d;

    /// <summary>Urutan tampil: belum selesai (urutan pengguna), lalu selesai (terbaru di atas).</summary>
    public IReadOnlyList<TodoItem> Items =>
        [.. _d.Items.Where(i => !i.Done), .. _d.Items.Where(i => i.Done).OrderByDescending(i => i.DoneAt)];

    public int DoneCount => _d.Items.Count(i => i.Done);
    public int Count => _d.Items.Count;

    public TodoItem? Add(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return null;
        if (text.Length > MaxLength) text = text[..MaxLength];

        var item = new TodoItem { Text = text };
        // Item baru masuk dari atas.
        _d.Items.Insert(0, item);
        Changed?.Invoke();
        return item;
    }

    public void Toggle(Guid id)
    {
        var item = _d.Items.FirstOrDefault(i => i.Id == id);
        if (item is null) return;
        item.Done = !item.Done;
        item.DoneAt = item.Done ? _time.GetUtcNow() : null;
        Changed?.Invoke();
    }

    public void Remove(Guid id)
    {
        if (_d.Items.RemoveAll(i => i.Id == id) > 0) Changed?.Invoke();
    }

    /// <summary>Pindahkan tugas belum selesai ke posisi lain (seret untuk mengurutkan).</summary>
    public void Move(Guid id, int newIndex)
    {
        var open = _d.Items.Where(i => !i.Done).ToList();
        var item = open.FirstOrDefault(i => i.Id == id);
        if (item is null) return;
        open.Remove(item);
        open.Insert(Math.Clamp(newIndex, 0, open.Count), item);
        _d.Items = [.. open, .. _d.Items.Where(i => i.Done)];
        Changed?.Invoke();
    }

    /// <summary>Hapus tugas yang diselesaikan sebelum hari ini (waktu lokal).</summary>
    public void CleanUpOldDone()
    {
        var today = _time.GetLocalNow().Date;
        int removed = _d.Items.RemoveAll(i => i.Done && i.DoneAt is { } at && at.ToOffset(_time.GetLocalNow().Offset).Date < today);
        if (removed > 0) Changed?.Invoke();
    }
}
