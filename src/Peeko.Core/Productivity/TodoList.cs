namespace Peeko.Core.Productivity;

public sealed class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public bool Done { get; set; }
    public DateTimeOffset? DoneAt { get; set; }
    /// <summary>Tenggat (waktu lokal saat dibuat). Tanpa jam: sepanjang hari itu.</summary>
    public DateTime? Due { get; set; }
    public bool DueHasTime { get; set; }
    public TaskRepeat Repeat { get; set; }
}

public sealed class TodoData
{
    public List<TodoItem> Items { get; set; } = [];
}

/// <summary>
/// Daftar tugas. Tugas belum selesai di atas: yang bertenggat urut waktu, lalu sisanya sesuai urutan
/// pengguna; yang selesai di bawah dan dibersihkan otomatis keesokan harinya (WaterTodo.dc.html).
/// Teks tugas dibaca dengan <see cref="DueParser"/>: "besok jam 7 kumpul laporan" langsung bertenggat.
/// Tugas berulang yang dicentang tidak selesai, tapi pindah ke kejadian berikutnya.
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

    private DateTime Now => _time.GetLocalNow().DateTime;

    /// <summary>Urutan tampil: belum selesai (bertenggat dulu), lalu selesai (terbaru di atas).</summary>
    public IReadOnlyList<TodoItem> Items =>
    [
        .. _d.Items.Where(i => !i.Done && i.Due is not null).OrderBy(i => i.Due),
        .. _d.Items.Where(i => !i.Done && i.Due is null),
        .. _d.Items.Where(i => i.Done).OrderByDescending(i => i.DoneAt),
    ];

    public int DoneCount => _d.Items.Count(i => i.Done);
    public int Count => _d.Items.Count;

    public TodoItem? Add(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return null;
        if (text.Length > MaxLength) text = text[..MaxLength];

        var parsed = DueParser.Parse(text, Now);
        var item = new TodoItem { Text = parsed.Text, Due = parsed.Due, DueHasTime = parsed.HasTime, Repeat = parsed.Repeat };
        // Item baru masuk dari atas.
        _d.Items.Insert(0, item);
        Changed?.Invoke();
        return item;
    }

    public TodoItem? Find(Guid id) => _d.Items.FirstOrDefault(i => i.Id == id);

    public void Toggle(Guid id)
    {
        var item = Find(id);
        if (item is null) return;
        if (!item.Done && item.Repeat != TaskRepeat.None && item.Due is { } due)
        {
            // Berulang: lompat ke kejadian berikutnya yang belum lewat.
            var next = DueParser.Advance(due, item.Repeat);
            while (next <= Now) next = DueParser.Advance(next, item.Repeat);
            item.Due = next;
            Changed?.Invoke();
            return;
        }
        item.Done = !item.Done;
        item.DoneAt = item.Done ? _time.GetUtcNow() : null;
        Changed?.Invoke();
    }

    /// <summary>Tunda: tenggat jadi sekarang + <paramref name="by"/>.</summary>
    public void Snooze(Guid id, TimeSpan by)
    {
        var item = Find(id);
        if (item is null || item.Done) return;
        var at = Now + by;
        item.Due = new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0);
        item.DueHasTime = true;
        Changed?.Invoke();
    }

    /// <summary>Tunda ke besok jam 07.00.</summary>
    public void SnoozeToMorning(Guid id)
    {
        var item = Find(id);
        if (item is null || item.Done) return;
        item.Due = Now.Date.AddDays(1).AddHours(7);
        item.DueHasTime = true;
        Changed?.Invoke();
    }

    public void ClearDue(Guid id)
    {
        var item = Find(id);
        if (item is null) return;
        item.Due = null;
        item.DueHasTime = false;
        item.Repeat = TaskRepeat.None;
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

    /// <summary>Tugas belum selesai yang punya jam tenggat, urut waktu.</summary>
    public IEnumerable<TodoItem> Timed => _d.Items.Where(i => !i.Done && i.Due is not null && i.DueHasTime).OrderBy(i => i.Due);

    /// <summary>Tugas bertenggat berikutnya (paling dekat, belum lewat lebih dari <paramref name="grace"/>).</summary>
    public TodoItem? Next(TimeSpan grace) => Timed.FirstOrDefault(i => i.Due > Now - grace);

    public bool IsOverdue(TodoItem item) =>
        !item.Done && item.Due is { } due && (item.DueHasTime ? due < Now : due.Date < Now.Date);

    /// <summary>Hapus tugas yang diselesaikan sebelum hari ini (waktu lokal).</summary>
    public void CleanUpOldDone()
    {
        var today = _time.GetLocalNow().Date;
        int removed = _d.Items.RemoveAll(i => i.Done && i.DoneAt is { } at && at.ToOffset(_time.GetLocalNow().Offset).Date < today);
        if (removed > 0) Changed?.Invoke();
    }
}
