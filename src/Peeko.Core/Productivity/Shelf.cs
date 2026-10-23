namespace Peeko.Core.Productivity;

public sealed class ShelfItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = "";
    /// <summary>true jika file disalin ke folder rak (sumbernya sementara, mis. dari browser).</summary>
    public bool IsCopy { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    /// <summary>Isi teks/tautan yang diseret ke rak; null untuk file.</summary>
    public string? Text { get; set; }

    public bool IsText => Text is not null;

    public bool IsLink => Text is not null && Shelf.AsLink(Text) is not null;
}

public sealed class ShelfData
{
    public List<ShelfItem> Items { get; set; } = [];
}

/// <summary>
/// Rak file: tempat singgah file saat berpindah antar-jendela. Yang disimpan adalah tautan ke file
/// aslinya, kecuali sumbernya folder sementara: file itu disalin ke folder rak supaya tidak hilang
/// (Shelf.dc.html).
/// </summary>
public sealed class Shelf
{
    private readonly TimeProvider _time;
    private readonly ShelfData _d;
    private readonly string _copyDir;
    private readonly string _tempDir;

    public Shelf(TimeProvider time, string copyDir, ShelfData? restored = null, string? tempDir = null)
    {
        _time = time;
        _copyDir = copyDir;
        _tempDir = Normalize(tempDir ?? System.IO.Path.GetTempPath());
        _d = restored ?? new ShelfData();
        RemoveMissing();
    }

    public event Action? Changed;

    public ShelfData Data => _d;

    /// <summary>Terbaru di depan.</summary>
    public IReadOnlyList<ShelfItem> Items => [.. _d.Items.OrderByDescending(i => i.AddedAt)];

    public int Add(IEnumerable<string> paths)
    {
        int added = 0;
        foreach (var raw in paths)
        {
            var path = System.IO.Path.GetFullPath(raw);
            if (!File.Exists(path) && !Directory.Exists(path)) continue;
            if (_d.Items.Any(i => !i.IsText && string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase))) continue;

            bool isTemp = Normalize(path).StartsWith(_tempDir, StringComparison.OrdinalIgnoreCase) && File.Exists(path);
            var stored = isTemp ? CopyToShelf(path) : path;
            if (stored is null) continue;

            _d.Items.Add(new ShelfItem { Path = stored, IsCopy = isTemp, AddedAt = _time.GetUtcNow() });
            added++;
        }
        if (added > 0) Changed?.Invoke();
        return added;
    }

    public const int MaxTextLength = 100_000;

    /// <summary>Simpan potongan teks atau tautan. Teks yang sama tidak disimpan dua kali.</summary>
    public bool AddText(string? text)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) return false;
        if (text.Length > MaxTextLength) text = text[..MaxTextLength];
        if (_d.Items.Any(i => i.Text == text)) return false;
        _d.Items.Add(new ShelfItem { Text = text, AddedAt = _time.GetUtcNow() });
        Changed?.Invoke();
        return true;
    }

    /// <summary>Tautan web (http/https, atau "www.…") dalam satu baris, atau null bila bukan tautan.</summary>
    public static Uri? AsLink(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || text.Any(char.IsWhiteSpace)) return null;
        if (text.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) text = "https://" + text;
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
    }

    /// <summary>Label kartu: domain untuk tautan, baris pertama untuk teks.</summary>
    public static string TextLabel(string text)
    {
        if (AsLink(text) is { } uri) return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        var line = text.Trim().Split('\n', 2)[0].Trim();
        return line.Length > 60 ? line[..60] + "…" : line;
    }

    public void Remove(Guid id)
    {
        var item = _d.Items.FirstOrDefault(i => i.Id == id);
        if (item is null) return;
        _d.Items.Remove(item);
        DeleteCopy(item);
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (_d.Items.Count == 0) return;
        foreach (var item in _d.Items) DeleteCopy(item);
        _d.Items.Clear();
        Changed?.Invoke();
    }

    /// <summary>Buang item yang filenya sudah tidak ada (dipindah/dihapus di luar rak).</summary>
    public void RemoveMissing()
    {
        int removed = _d.Items.RemoveAll(i => !i.IsText && !File.Exists(i.Path) && !Directory.Exists(i.Path));
        if (removed > 0) Changed?.Invoke();
    }

    public long TotalBytes => _d.Items.Sum(i =>
    {
        try { return !i.IsText && File.Exists(i.Path) ? new FileInfo(i.Path).Length : 0; }
        catch (IOException) { return 0; }
    });

    private string? CopyToShelf(string path)
    {
        try
        {
            var folder = System.IO.Path.Combine(_copyDir, Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            var target = System.IO.Path.Combine(folder, System.IO.Path.GetFileName(path));
            File.Copy(path, target);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void DeleteCopy(ShelfItem item)
    {
        if (!item.IsCopy || item.IsText) return; // tautan: file asli pengguna tidak pernah dihapus
        try
        {
            if (File.Exists(item.Path)) File.Delete(item.Path);
            var folder = System.IO.Path.GetDirectoryName(item.Path);
            if (folder is not null && Normalize(folder).StartsWith(Normalize(_copyDir), StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                Directory.Delete(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string Normalize(string path) =>
        System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;

    /// <summary>"12,4 MB".</summary>
    public static string FormatSize(long bytes)
    {
        var id = System.Globalization.CultureInfo.GetCultureInfo("id-ID");
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => (bytes / 1024.0).ToString("0", id) + " KB",
            < 1024L * 1024 * 1024 => (bytes / 1024.0 / 1024).ToString("0.0", id) + " MB",
            _ => (bytes / 1024.0 / 1024 / 1024).ToString("0.0", id) + " GB",
        };
    }
}
