using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Peeko.Core.Updates;

/// <param name="Version">Versi rilis (dari tag, mis. "v0.1.2" → 0.1.2).</param>
/// <param name="AssetUrl">Tautan unduh zip untuk Windows x64.</param>
/// <param name="PageUrl">Halaman rilis di GitHub.</param>
public sealed record ReleaseInfo(Version Version, string Tag, string AssetUrl, string AssetName, long AssetSize, string PageUrl);

/// <summary>
/// Mengecek rilis terbaru di GitHub (<c>/repos/{owner}/{repo}/releases/latest</c>). Rilis draft dan
/// prerelease tidak dihitung oleh endpoint itu. Yang dikirim hanya permintaan biasa ke GitHub.
/// Memakai ETag: jika rilis tidak berubah GitHub membalas 304 yang tidak memakan kuota 60
/// request/jam, sehingga aman dicek sering.
/// </summary>
public sealed class UpdateChecker
{
    public const string DefaultFeed = "https://api.github.com/repos/mezlpn/peeko/releases/latest";
    private const string AssetSuffix = "-win-x64.zip";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _feed;
    private string? _etag;
    private ReleaseInfo? _lastResult;

    public UpdateChecker(HttpClient http, string? feed = null)
    {
        _http = http;
        _feed = feed ?? DefaultFeed;
    }

    /// <summary>Rilis terbaru, atau null jika tidak ada/tidak punya zip Windows.</summary>
    /// <exception cref="HttpRequestException">Jaringan/server bermasalah.</exception>
    public async Task<ReleaseInfo?> GetLatestAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _feed);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        if (_etag is not null) request.Headers.TryAddWithoutValidation("If-None-Match", _etag);
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotModified) return _lastResult;
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ReleaseDto>(Json, ct);
        _lastResult = ToRelease(dto);
        _etag = response.Headers.ETag?.ToString();
        return _lastResult;
    }

    private static ReleaseInfo? ToRelease(ReleaseDto? dto)
    {
        if (dto is null || dto.Draft || dto.Prerelease || TryParseVersion(dto.TagName) is not { } version) return null;

        var asset = dto.Assets?.FirstOrDefault(a => a.Name.EndsWith(AssetSuffix, StringComparison.OrdinalIgnoreCase));
        if (asset is null) return null;

        return new ReleaseInfo(version, dto.TagName, asset.BrowserDownloadUrl, asset.Name, asset.Size, dto.HtmlUrl ?? "");
    }

    /// <summary>"v0.1.2" / "0.1.2" / "v1.0" → Version dengan 3 komponen; null jika bukan versi.</summary>
    public static Version? TryParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var text = tag.Trim().TrimStart('v', 'V');
        int dash = text.IndexOfAny(['-', '+']);
        if (dash >= 0) text = text[..dash];
        if (!Version.TryParse(text.Contains('.') ? text : text + ".0", out var v)) return null;
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
    }

    /// <summary>Apakah <paramref name="latest"/> lebih baru dari versi yang sedang berjalan (bandingkan 3 komponen).</summary>
    public static bool IsNewer(Version current, Version latest) =>
        new Version(current.Major, current.Minor, Math.Max(current.Build, 0)) < latest;

    private sealed record ReleaseDto(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        bool Draft,
        bool Prerelease,
        List<AssetDto>? Assets);

    private sealed record AssetDto(
        string Name,
        long Size,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
