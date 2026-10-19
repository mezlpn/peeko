namespace Peeko.Core.Conversion;

/// <summary>
/// Memilih beberapa format tujuan yang paling sering dipakai untuk ditampilkan sebagai chip; sisanya
/// ada di "Lainnya". Filee bisa mengubah PNG ke 40+ format, tapi yang dicari orang biasanya 4–5 saja.
/// </summary>
public static class ConvertSuggestions
{
    private static readonly HashSet<string> Images = ["png", "jpg", "jpeg", "jpe", "jfif", "webp", "gif", "bmp", "tif", "tiff", "heic", "heif", "avif", "ico", "svg", "psd", "jxl", "tga", "raw", "cr2", "nef", "arw", "dng"];
    private static readonly HashSet<string> Documents = ["doc", "docx", "docm", "odt", "rtf", "txt", "md", "html", "htm", "hwp", "hwpx", "pages", "epub", "mobi", "azw3", "fb2", "eml", "msg"];
    private static readonly HashSet<string> Sheets = ["xls", "xlsx", "xlsm", "ods", "csv", "tsv", "numbers"];
    private static readonly HashSet<string> Slides = ["ppt", "pptx", "pptm", "odp", "key"];
    private static readonly HashSet<string> Videos = ["mp4", "m4v", "mov", "mkv", "webm", "avi", "wmv", "flv", "mpg", "mpeg", "ts", "m2ts", "3gp", "ogv", "vob"];
    private static readonly HashSet<string> Audio = ["mp3", "m4a", "aac", "wav", "flac", "ogg", "opus", "wma", "aiff", "amr"];

    private static readonly string[] General = ["pdf", "png", "jpg", "docx", "webp", "mp3", "mp4", "xlsx", "txt", "zip"];

    public static IReadOnlyList<string> PreferredFor(string sourceExtension) => Normalize(sourceExtension) switch
    {
        "pdf" => ["docx", "png", "jpg", "txt", "pptx"],
        var e when Images.Contains(e) => ["png", "jpg", "webp", "pdf", "ico", "gif"],
        var e when Sheets.Contains(e) => ["pdf", "xlsx", "csv", "json"],
        var e when Slides.Contains(e) => ["pdf", "png", "jpg"],
        var e when Documents.Contains(e) => ["pdf", "docx", "txt", "html", "epub"],
        var e when Videos.Contains(e) => ["mp4", "mp3", "gif", "webm", "m4a"],
        var e when Audio.Contains(e) => ["mp3", "m4a", "wav", "flac", "ogg"],
        _ => General,
    };

    /// <summary>
    /// Maksimal <paramref name="max"/> format: urutan favorit untuk jenis file sumbernya, lalu diisi
    /// dari daftar umum. Hanya format yang memang ada di <paramref name="available"/>.
    /// </summary>
    public static IReadOnlyList<string> Pick(IReadOnlyCollection<string> available, IEnumerable<string> sourceExtensions, int max = 5)
    {
        var sources = sourceExtensions.Select(Normalize).Distinct().ToList();
        var preferred = sources.Count == 1 ? PreferredFor(sources[0]) : General;
        var set = new HashSet<string>(available, StringComparer.OrdinalIgnoreCase);
        // Tidak ada yang cocok dengan daftar favorit: ambil saja dari urutan Filee.
        return preferred.Concat(General).Concat(available.Select(Normalize))
            .Distinct()
            // Format sumbernya sendiri disembunyikan (PNG → PNG tidak berguna), kecuali saat jenis filenya campuran.
            .Where(f => set.Contains(f) && !(sources.Count == 1 && sources[0] == f))
            .Take(max)
            .ToList();
    }

    /// <summary>Format tujuan yang bisa untuk semua file sekaligus (urutan dari daftar pertama).</summary>
    public static IReadOnlyList<string> Intersect(IReadOnlyList<IReadOnlyList<string>> lists)
    {
        if (lists.Count == 0) return [];
        IEnumerable<string> result = lists[0];
        foreach (var other in lists.Skip(1)) result = result.Intersect(other, StringComparer.OrdinalIgnoreCase);
        return result.ToList();
    }

    /// <summary>".JPG" → "jpg".</summary>
    public static string Normalize(string extension) => extension.Trim().TrimStart('.').ToLowerInvariant();
}
