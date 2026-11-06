namespace Peeko.Core.Productivity;

public sealed class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class NotesData
{
    public List<Note> Notes { get; set; } = [];
}

/// <summary>Catatan singkat. Judul = baris pertama; yang terakhir diubah di atas.</summary>
public sealed class NoteBook
{
    public const int MaxLength = 20_000;
    public const int MaxNotes = 200;

    private readonly TimeProvider _time;
    private readonly NotesData _d;

    public NoteBook(TimeProvider time, NotesData? restored = null)
    {
        _time = time;
        _d = restored ?? new NotesData();
    }

    public event Action? Changed;

    public NotesData Data => _d;

    public IReadOnlyList<Note> Notes => [.. _d.Notes.OrderByDescending(n => n.UpdatedAt)];

    public Note? Find(Guid id) => _d.Notes.FirstOrDefault(n => n.Id == id);

    public Note? Add(string text = "")
    {
        if (_d.Notes.Count >= MaxNotes) return null;
        var note = new Note { Text = Trim(text), UpdatedAt = _time.GetUtcNow() };
        _d.Notes.Add(note);
        Changed?.Invoke();
        return note;
    }

    public void Update(Guid id, string text)
    {
        var note = Find(id);
        text = Trim(text);
        if (note is null || note.Text == text) return;
        note.Text = text;
        note.UpdatedAt = _time.GetUtcNow();
        Changed?.Invoke();
    }

    public void Remove(Guid id)
    {
        if (_d.Notes.RemoveAll(n => n.Id == id) > 0) Changed?.Invoke();
    }

    /// <summary>Hapus catatan kosong (mis. dibuat lalu ditinggal), kecuali <paramref name="keep"/>.</summary>
    public void RemoveEmpty(Guid? keep = null)
    {
        if (_d.Notes.RemoveAll(n => n.Id != keep && string.IsNullOrWhiteSpace(n.Text)) > 0) Changed?.Invoke();
    }

    public static string Title(Note note)
    {
        var first = note.Text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        return first is null ? "Catatan baru" : first.Length > 60 ? first[..60] : first;
    }

    private static string Trim(string text) => text.Length > MaxLength ? text[..MaxLength] : text.Replace("\r\n", "\n");
}

public sealed record Flashcard(string Question, string Answer);

/// <summary>
/// Flashcard dari catatan: tiap baris "pertanyaan :: jawaban" jadi satu kartu. Sesi belajar mengacak
/// kartu; "Lagi" menaruh kartu kembali di antrean, "Hafal" mengeluarkannya.
/// </summary>
public sealed class StudySession
{
    private readonly List<Flashcard> _queue;

    public StudySession(IEnumerable<Flashcard> cards, Random? random = null)
    {
        var rng = random ?? Random.Shared;
        _queue = [.. cards.OrderBy(_ => rng.Next())];
        Total = _queue.Count;
    }

    public int Total { get; }

    public int Known { get; private set; }

    public int Again { get; private set; }

    public Flashcard? Current => _queue.Count > 0 ? _queue[0] : null;

    public bool Flipped { get; private set; }

    public bool Finished => _queue.Count == 0;

    public void Flip() => Flipped = !Flipped;

    public void MarkKnown()
    {
        if (Finished) return;
        _queue.RemoveAt(0);
        Known++;
        Flipped = false;
    }

    /// <summary>Belum hafal: kartu kembali ke antrean, beberapa kartu di belakang.</summary>
    public void MarkAgain()
    {
        if (Finished) return;
        var card = _queue[0];
        _queue.RemoveAt(0);
        _queue.Insert(Math.Min(_queue.Count, 3), card);
        Again++;
        Flipped = false;
    }

    public static IReadOnlyList<Flashcard> Parse(string text) =>
    [
        .. text.Split('\n')
            .Select(l => l.Trim().TrimStart('-', '•', '*').Trim())
            .Select(l => l.Split("::", 2))
            .Where(p => p.Length == 2 && p[0].Trim().Length > 0 && p[1].Trim().Length > 0)
            .Select(p => new Flashcard(p[0].Trim(), p[1].Trim())),
    ];
}
