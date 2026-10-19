using System.Text.Json;
using System.Text.Json.Serialization;

namespace Peeko.Core.Conversion;

/// <param name="Name">Nama format sumber, mis. "PNG".</param>
/// <param name="Ready">Format tujuan yang langsung bisa.</param>
/// <param name="NeedsEngine">Format tujuan yang butuh mesin tambahan Filee (FFmpeg, LibreOffice, …).</param>
public sealed record FormatTargets(string Name, IReadOnlyList<string> Ready, IReadOnlyList<string> NeedsEngine);

/// <param name="Done">true jika berhasil dikonversi.</param>
/// <param name="Outputs">File hasil (bisa lebih dari satu, mis. PDF → PNG per halaman).</param>
public sealed record FileOutcome(string Source, string State, IReadOnlyList<string> Outputs, string? Error)
{
    public bool Done => State.Equals("done", StringComparison.OrdinalIgnoreCase);
}

public sealed record ConvertResult(string Target, int Converted, int Skipped, int Failed, IReadOnlyList<FileOutcome> Files)
{
    public IEnumerable<string> Outputs => Files.Where(f => f.Done).SelectMany(f => f.Outputs);

    /// <summary>Pesan galat pertama, untuk ditampilkan ke pengguna.</summary>
    public string? FirstError => Files.FirstOrDefault(f => !f.Done && !string.IsNullOrWhiteSpace(f.Error))?.Error;
}

/// <summary>
/// Membaca keluaran <c>filee-cli</c> (Filee, MIT, github.com/KnifeLemon/Filee): daftar format dari
/// <c>filee formats &lt;ext&gt;</c> dan hasil dari <c>filee convert … --json</c>.
/// </summary>
public static class FileeOutput
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// <code>
    /// PNG (png) converts to:
    ///   jpg, webp, …
    /// With optional engines (install them in Filee, Settings → Engines):
    ///   heic, eps, …
    /// </code>
    /// null jika formatnya tidak dikenal Filee.
    /// </summary>
    public static FormatTargets? ParseFormats(string text)
    {
        string? name = null;
        var ready = new List<string>();
        var optional = new List<string>();
        List<string>? current = null;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            if (!char.IsWhiteSpace(line[0]))
            {
                if (line.Contains("converts to", StringComparison.OrdinalIgnoreCase))
                {
                    int paren = line.IndexOf('(');
                    name = (paren > 0 ? line[..paren] : line.Split(' ')[0]).Trim();
                    current = ready;
                }
                else if (line.StartsWith("With optional engines", StringComparison.OrdinalIgnoreCase))
                {
                    current = optional;
                }
                else
                {
                    current = null;
                }
                continue;
            }
            // "(nothing with the engines installed now)" bukan format.
            if (line.TrimStart().StartsWith('(')) continue;
            current?.AddRange(line.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(f => f.ToLowerInvariant()));
        }

        return name is null ? null : new FormatTargets(name, ready.Distinct().ToList(), optional.Distinct().Except(ready).ToList());
    }

    /// <exception cref="JsonException">Bukan JSON hasil konversi.</exception>
    public static ConvertResult ParseResult(string json)
    {
        var dto = JsonSerializer.Deserialize<ResultDto>(json, Json) ?? throw new JsonException("Hasil kosong.");
        var files = (dto.Files ?? [])
            .Select(f => new FileOutcome(f.Source ?? "", f.State ?? "", f.Outputs ?? [], f.Error))
            .ToList();
        return new ConvertResult(dto.Target ?? "", dto.Converted, dto.Skipped, dto.Failed, files);
    }

    private sealed record ResultDto(string? Target, int Converted, int Skipped, int Failed, List<FileDto>? Files);

    private sealed record FileDto(
        string? Source,
        string? State,
        List<string>? Outputs,
        [property: JsonPropertyName("error")] string? Error);
}
