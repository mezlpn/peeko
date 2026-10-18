using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Celah.Core.Lyrics;

public abstract record LyricsResult
{
    public sealed record Synced(SyncedLyrics Lyrics) : LyricsResult;

    /// <summary>LRCLIB hanya punya teks tanpa waktu.</summary>
    public sealed record Plain(string Text) : LyricsResult;

    public sealed record Instrumental : LyricsResult;

    public sealed record NotFound : LyricsResult;

    /// <summary>Gagal karena jaringan/server; tidak di-cache supaya dicoba lagi lain kali.</summary>
    public sealed record Failed(string Reason) : LyricsResult;
}

/// <summary>
/// Mengambil lirik dari LRCLIB (https://lrclib.net) dengan cache di disk.
/// Yang dikirim ke server hanya judul, artis, album, dan durasi lagu.
/// </summary>
public sealed class LyricsService
{
    public const string BaseUrl = "https://lrclib.net";
    private static readonly TimeSpan NotFoundTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan DurationTolerance = TimeSpan.FromSeconds(3);
    private const int MaxQueries = 3;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _cacheDir;
    private readonly TimeProvider _time;

    public LyricsService(HttpClient http, string cacheDir, TimeProvider time)
    {
        _http = http;
        _cacheDir = cacheDir;
        _time = time;
    }

    /// <param name="queries">Kandidat dari <see cref="TrackQueryBuilder"/>; yang pertama menentukan kunci cache.</param>
    /// <param name="retryNotFound">"Cari lagi": abaikan cache "tidak ditemukan".</param>
    public async Task<LyricsResult> GetAsync(IReadOnlyList<TrackQuery> queries, bool retryNotFound, CancellationToken ct)
    {
        if (queries.Count == 0) return new LyricsResult.NotFound();

        var cacheFile = CachePath(queries[0].CacheKey);
        if (ReadCache(cacheFile) is { } cached)
        {
            bool staleNotFound = cached.Kind == "notfound"
                && (retryNotFound || _time.GetUtcNow() - cached.SavedAt > NotFoundTtl);
            if (!staleNotFound) return ToResult(cached);
        }

        try
        {
            var record = await FetchAsync(queries, ct);
            var entry = record is null
                ? new CacheEntry("notfound", null, null, _time.GetUtcNow())
                : record.Instrumental
                    ? new CacheEntry("instrumental", null, null, _time.GetUtcNow())
                    : new CacheEntry(
                        string.IsNullOrWhiteSpace(record.SyncedLyrics) ? "plain" : "synced",
                        record.SyncedLyrics, record.PlainLyrics, _time.GetUtcNow());

            WriteCache(cacheFile, entry);
            return ToResult(entry);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            return new LyricsResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Lirik tersinkron selalu diutamakan: hasil yang hanya berisi teks biasa (atau instrumental)
    /// disimpan sebagai cadangan sambil kandidat lain tetap dicoba.
    /// </summary>
    private async Task<LrclibRecord?> FetchAsync(IReadOnlyList<TrackQuery> queries, CancellationToken ct)
    {
        var tries = queries.Take(MaxQueries).ToList();
        LrclibRecord? fallback = null;

        // 1. Pencocokan persis (judul + artis + durasi) — paling akurat.
        foreach (var q in tries)
        {
            if (q.Duration <= TimeSpan.Zero || q.Artist.Length == 0) continue;
            var url = $"{BaseUrl}/api/get?track_name={E(q.Title)}&artist_name={E(q.Artist)}"
                    + $"&duration={(int)Math.Round(q.Duration.TotalSeconds)}"
                    + (q.Album.Length > 0 ? $"&album_name={E(q.Album)}" : "");
            using var response = await _http.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) continue;
            response.EnsureSuccessStatusCode();
            var record = await response.Content.ReadFromJsonAsync<LrclibRecord>(Json, ct);
            if (record is null || !HasContent(record)) continue;
            if (IsSynced(record)) return record;
            fallback ??= record;
        }

        // 2. Pencarian bebas, pilih yang durasinya cocok dan punya lirik berwaktu.
        foreach (var q in tries)
        {
            var url = $"{BaseUrl}/api/search?track_name={E(q.Title)}"
                    + (q.Artist.Length > 0 ? $"&artist_name={E(q.Artist)}" : "");
            using var response = await _http.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) continue;
            response.EnsureSuccessStatusCode();
            var results = await response.Content.ReadFromJsonAsync<List<LrclibRecord>>(Json, ct) ?? [];
            var best = PickBest(results, q.Duration);
            if (best is null) continue;
            if (IsSynced(best)) return best;
            fallback ??= best;
        }

        return fallback;
    }

    private static bool IsSynced(LrclibRecord r) => !r.Instrumental && !string.IsNullOrWhiteSpace(r.SyncedLyrics);

    internal static LrclibRecord? PickBest(IEnumerable<LrclibRecord> results, TimeSpan duration) =>
        results
            .Where(HasContent)
            .Where(r => duration <= TimeSpan.Zero
                        || (r.Duration is { } d && (TimeSpan.FromSeconds(d) - duration).Duration() <= DurationTolerance))
            .OrderByDescending(r => !string.IsNullOrWhiteSpace(r.SyncedLyrics))
            .ThenBy(r => r.Duration is { } d && duration > TimeSpan.Zero
                ? (TimeSpan.FromSeconds(d) - duration).Duration()
                : TimeSpan.Zero)
            .FirstOrDefault();

    private static bool HasContent(LrclibRecord r) =>
        r.Instrumental || !string.IsNullOrWhiteSpace(r.SyncedLyrics) || !string.IsNullOrWhiteSpace(r.PlainLyrics);

    private static string E(string s) => Uri.EscapeDataString(s);

    private static LyricsResult ToResult(CacheEntry e) => e.Kind switch
    {
        "synced" when e.Synced is { } s && LrcParser.Parse(s) is { Lines.Count: > 0 } parsed => new LyricsResult.Synced(parsed),
        "synced" or "plain" when !string.IsNullOrWhiteSpace(e.Plain) => new LyricsResult.Plain(e.Plain!),
        "instrumental" => new LyricsResult.Instrumental(),
        _ => new LyricsResult.NotFound(),
    };

    // ── Cache ────────────────────────────────────────────────────────────────

    private string CachePath(string key)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_cacheDir, hash[..16].ToLower(CultureInfo.InvariantCulture) + ".json");
    }

    private static CacheEntry? ReadCache(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null; // cache rusak: anggap tidak ada
        }
    }

    private static void WriteCache(string path, CacheEntry entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(entry, Json));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cache hanya optimasi; gagal menulis tidak boleh menggagalkan tampilan lirik.
        }
    }

    internal sealed record CacheEntry(string Kind, string? Synced, string? Plain, DateTimeOffset SavedAt);

    internal sealed record LrclibRecord(
        string? TrackName,
        string? ArtistName,
        double? Duration,
        bool Instrumental,
        string? PlainLyrics,
        string? SyncedLyrics);
}
