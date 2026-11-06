using System.Text.Json.Serialization;

namespace Peeko.Core.Media;

/// <summary>Stasiun radio dari Radio Browser (nama properti JSON mengikuti API-nya).</summary>
public sealed record RadioStation(
    [property: JsonPropertyName("stationuuid")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url_resolved")] string Url,
    [property: JsonPropertyName("tags")] string? Tags,
    [property: JsonPropertyName("codec")] string? Codec,
    [property: JsonPropertyName("bitrate")] int Bitrate,
    [property: JsonPropertyName("countrycode")] string? Country)
{
    public string Detail
    {
        get
        {
            var tags = string.Join(", ", (Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2));
            var quality = Bitrate > 0 ? $"{Bitrate} kbps" : Codec is { Length: > 0 } c && c != "UNKNOWN" ? c : "";
            return string.Join(" · ", new[] { tags, quality }.Where(s => s.Length > 0));
        }
    }
}
