using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Peeko.Core.Updates;

namespace Peeko.Core.Conversion;

/// <param name="ZipUrl">Tautan Filee-x.y.z-win-Portable.zip.</param>
/// <param name="SumsUrl">Tautan daftar SHA-256 resmi rilis yang sama.</param>
public sealed record FileePackage(Version Version, string ZipName, string ZipUrl, long ZipSize, string SumsUrl);

/// <summary>
/// Rilis Filee terbaru dari GitHub (<c>KnifeLemon/Filee</c>). Peeko tidak membundel Filee: paket
/// portable resminya diunduh saat pengguna memasang fitur Ubah format, lalu dicek dengan SHA-256 dari
/// rilis yang sama.
/// </summary>
public sealed class FileeRelease
{
    public const string Repository = "https://github.com/KnifeLemon/Filee";
    public const string DefaultFeed = "https://api.github.com/repos/KnifeLemon/Filee/releases/latest";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _feed;

    public FileeRelease(HttpClient http, string? feed = null)
    {
        _http = http;
        _feed = feed ?? DefaultFeed;
    }

    /// <summary>Paket portable Windows terbaru, atau null jika rilis tidak memuatnya.</summary>
    /// <exception cref="HttpRequestException">Jaringan/server bermasalah.</exception>
    public async Task<FileePackage?> GetLatestAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _feed);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ReleaseDto>(Json, ct);
        return dto is null ? null : Pick(dto.TagName, dto.Assets ?? []);
    }

    private static FileePackage? Pick(string? tag, List<AssetDto> assets)
    {
        if (UpdateChecker.TryParseVersion(tag) is not { } version) return null;
        var zip = assets.FirstOrDefault(a => a.Name.EndsWith("-win-Portable.zip", StringComparison.OrdinalIgnoreCase));
        // "Filee-1.12.0-SHA256SUMS.txt" memuat semua paket; yang per-platform (linux-x64-…) tidak memuat zip Windows.
        var sums = assets.FirstOrDefault(a => a.Name.Equals($"Filee-{version.ToString(3)}-SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
            ?? assets.FirstOrDefault(a => a.Name.EndsWith("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)
                                          && !a.Name.Contains("linux", StringComparison.OrdinalIgnoreCase)
                                          && !a.Name.Contains("osx", StringComparison.OrdinalIgnoreCase));
        if (zip is null || sums is null) return null;
        return new FileePackage(version, zip.Name, zip.BrowserDownloadUrl, zip.Size, sums.BrowserDownloadUrl);
    }

    /// <summary>Untuk test: pilih paket dari JSON rilis GitHub.</summary>
    public static FileePackage? ParseRelease(string json)
    {
        var dto = JsonSerializer.Deserialize<ReleaseDto>(json, Json);
        return dto is null ? null : Pick(dto.TagName, dto.Assets ?? []);
    }

    /// <summary>
    /// Baris format <c>sha256sum</c>: "&lt;hex&gt;  &lt;nama file&gt;" (nama boleh diawali '*').
    /// Mengembalikan hash huruf kecil, atau null jika file tidak ada di daftar.
    /// </summary>
    public static string? FindChecksum(string sumsText, string fileName)
    {
        foreach (var raw in sumsText.Split('\n'))
        {
            var line = raw.Trim();
            int space = line.IndexOfAny([' ', '\t']);
            if (space != 64) continue;
            var name = line[space..].Trim().TrimStart('*');
            if (name.Equals(fileName, StringComparison.OrdinalIgnoreCase)) return line[..64].ToLowerInvariant();
        }
        return null;
    }

    private sealed record ReleaseDto([property: JsonPropertyName("tag_name")] string? TagName, List<AssetDto>? Assets);

    private sealed record AssetDto(string Name, long Size, [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
