namespace Peeko.Core.Productivity;

public sealed class Bookmark
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Kosong = tanpa kategori.</summary>
    public string Category { get; set; } = "";
}

public sealed class BookmarkData
{
    public List<Bookmark> Items { get; set; } = [];
}

/// <summary>
/// Tautan favorit per kategori. Ditambah dengan satu baris teks: alamat, judul (boleh kosong), dan
/// kategori dengan tanda pagar, mis. "github.com/mezlpn/peeko Repo Peeko #Kerja".
/// </summary>
public sealed class Bookmarks
{
    public const int Max = 100;

    private readonly BookmarkData _d;

    public Bookmarks(BookmarkData? restored = null)
    {
        _d = restored ?? new BookmarkData();
    }

    public BookmarkData Data => _d;

    public IReadOnlyList<Bookmark> Items => _d.Items;

    public event Action? Changed;

    /// <summary>Kategori yang dipakai, urut abjad.</summary>
    public IReadOnlyList<string> Categories =>
        [.. _d.Items.Select(b => b.Category).Where(c => c.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)];

    public IReadOnlyList<Bookmark> In(string? category) =>
        category is null ? _d.Items : [.. _d.Items.Where(b => string.Equals(b.Category, category, StringComparison.CurrentCultureIgnoreCase))];

    /// <returns>Tautan baru, atau null jika tidak ada alamat yang valid / daftar penuh.</returns>
    public Bookmark? Add(string input)
    {
        if (Parse(input) is not { } b || _d.Items.Count >= Max) return null;
        var existing = _d.Items.FirstOrDefault(x => string.Equals(x.Url, b.Url, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            // Alamat sama: perbarui judul/kategori saja.
            existing.Title = b.Title;
            if (b.Category.Length > 0) existing.Category = b.Category;
            Changed?.Invoke();
            return existing;
        }
        _d.Items.Add(b);
        Changed?.Invoke();
        return b;
    }

    public void Remove(Guid id)
    {
        if (_d.Items.RemoveAll(b => b.Id == id) > 0) Changed?.Invoke();
    }

    public static Bookmark? Parse(string input)
    {
        var words = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
        string category = "";
        var tag = words.LastOrDefault(w => w.StartsWith('#') && w.Length > 1);
        if (tag is not null)
        {
            category = tag[1..].Replace('_', ' ');
            words.Remove(tag);
        }
        int urlIndex = words.FindIndex(LooksLikeUrl);
        if (urlIndex < 0) return null;
        var raw = words[urlIndex];
        words.RemoveAt(urlIndex);
        if (!raw.Contains("://")) raw = "https://" + raw;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return null;
        var title = string.Join(' ', words);
        if (title.Length == 0) title = uri.Host.StartsWith("www.") ? uri.Host[4..] : uri.Host;
        if (category.Length > 0) category = char.ToUpper(category[0]) + category[1..];
        return new Bookmark { Url = uri.ToString(), Title = title, Category = category };
    }

    private static bool LooksLikeUrl(string word) =>
        word.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || word.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || (word.Contains('.') && !word.StartsWith('.') && !word.EndsWith('.') && word.IndexOf('.') < word.Length - 2
            && word.Split('/')[0].Split('.').Last().All(char.IsLetter));
}
