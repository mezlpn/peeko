using System.Text.RegularExpressions;

namespace Peeko.Core.Productivity;

public enum AiActionKind
{
    Task,
    Note,
    Music,
    Volume,
    Open,
}

/// <summary>Satu aksi yang diminta AI, mis. tambah tugas "besok jam 7 kumpul laporan".</summary>
public sealed record AiAction(AiActionKind Kind, string Value);

/// <summary>
/// Protokol aksi asisten Peeko. Model diminta menulis aksi sebagai baris khusus
/// <c>[[tugas: …]]</c>, <c>[[catatan: …]]</c>, <c>[[musik: putar|jeda|lanjut|sebelumnya]]</c>,
/// <c>[[volume: 0-100]]</c>, <c>[[buka: nama aplikasi]]</c>. Baris itu dipisahkan dari jawaban yang
/// ditampilkan. Aksi lain/tidak dikenal diabaikan, jadi model tidak bisa menjalankan perintah bebas.
/// </summary>
public static partial class AiActions
{
    public const string SystemPrompt =
        "Kamu asisten kecil di dalam Peeko, aplikasi notch di laptop Windows. Jawab singkat (maksimal 4 kalimat), " +
        "dalam bahasa yang dipakai pengguna. Kalau pengguna meminta sesuatu yang bisa kamu lakukan, tulis aksinya " +
        "di baris tersendiri, persis dengan format ini:\n" +
        "[[tugas: teks tugas, boleh dengan waktu seperti 'besok jam 7' atau 'tiap senin']]\n" +
        "[[catatan: isi catatan]]\n" +
        "[[musik: putar|jeda|lanjut|sebelumnya]]\n" +
        "[[volume: angka 0-100]]\n" +
        "[[buka: nama aplikasi]]\n" +
        "Jangan menulis aksi kalau tidak diminta. Selain aksi itu kamu tidak bisa mengubah apa pun di laptop.";

    public const string TranslatePrompt =
        "Terjemahkan teks pengguna. Kalau teksnya bahasa Indonesia, terjemahkan ke bahasa Inggris; selain itu " +
        "terjemahkan ke bahasa Indonesia. Tulis terjemahannya saja, tanpa penjelasan.";

    [GeneratedRegex(@"\[\[\s*(?<k>tugas|catatan|musik|volume|buka)\s*:\s*(?<v>[^\]]+?)\s*\]\]", RegexOptions.IgnoreCase)]
    private static partial Regex ActionRegex();

    /// <summary>Pisahkan jawaban yang ditampilkan dari aksi-aksinya.</summary>
    public static (string Text, IReadOnlyList<AiAction> Actions) Parse(string reply)
    {
        var actions = new List<AiAction>();
        var text = ActionRegex().Replace(reply, m =>
        {
            var value = m.Groups["v"].Value.Trim();
            AiAction? action = m.Groups["k"].Value.ToLowerInvariant() switch
            {
                "tugas" when value.Length > 0 => new(AiActionKind.Task, value),
                "catatan" when value.Length > 0 => new(AiActionKind.Note, value.Replace("\\n", "\n")),
                "musik" when MusicVerb(value) is { } verb => new(AiActionKind.Music, verb),
                "volume" when int.TryParse(value.TrimEnd('%'), out var v) => new(AiActionKind.Volume, Math.Clamp(v, 0, 100).ToString()),
                "buka" when value.Length is > 0 and <= 60 => new(AiActionKind.Open, value),
                _ => null,
            };
            if (action is not null) actions.Add(action);
            return "";
        });
        // Rapikan baris kosong yang tersisa setelah aksi dibuang.
        text = MultiBlank().Replace(text, "\n\n").Trim();
        return (text, actions.Take(5).ToList());
    }

    [GeneratedRegex(@"\n\s*\n(\s*\n)+")]
    private static partial Regex MultiBlank();

    private static string? MusicVerb(string value) => value.Trim().ToLowerInvariant() switch
    {
        "putar" or "play" or "lanjutkan" => "putar",
        "jeda" or "pause" or "stop" or "berhenti" => "jeda",
        "lanjut" or "next" or "berikutnya" => "lanjut",
        "sebelumnya" or "previous" or "prev" => "sebelumnya",
        _ => null,
    };

    /// <summary>Label tombol aksi, mis. "Tugas ditambah".</summary>
    public static string Label(AiAction a) => a.Kind switch
    {
        AiActionKind.Task => $"Tugas: {a.Value}",
        AiActionKind.Note => "Catatan ditambah",
        AiActionKind.Music => a.Value switch { "putar" => "Musik diputar", "jeda" => "Musik dijeda", "lanjut" => "Lagu berikutnya", _ => "Lagu sebelumnya" },
        AiActionKind.Volume => $"Volume {a.Value}%",
        _ => $"Membuka {a.Value}",
    };
}
