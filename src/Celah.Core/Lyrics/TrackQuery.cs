using System.Text.RegularExpressions;

namespace Celah.Core.Lyrics;

/// <summary>Kueri pencarian lirik untuk satu lagu.</summary>
public sealed record TrackQuery(string Title, string Artist, string Album, TimeSpan Duration)
{
    /// <summary>Kunci cache yang tidak bergantung pada sumber pemutar.</summary>
    public string CacheKey =>
        $"{Normalize(Artist)}|{Normalize(Title)}|{(int)Math.Round(Duration.TotalSeconds)}";

    private static string Normalize(string s) => s.Trim().ToLowerInvariant();
}

/// <summary>
/// Merapikan judul dari pemutar sebelum dicari di LRCLIB. Video YouTube sering berjudul
/// "Artis - Judul (Official Video)" dengan nama kanal sebagai artis.
/// </summary>
public static partial class TrackQueryBuilder
{
    // Imbuhan yang bukan bagian dari judul lagu.
    [GeneratedRegex(@"\s*[\(\[【]\s*(official\s*(music\s*)?(video|audio|lyric\s*video|visuali[sz]er|mv)|lyrics?(\s*video)?|lirik|video\s*(klip|lirik)|audio|mv|m/v|hd|hq|4k|visuali[sz]er|live\s*session)[^\)\]】]*[\)\]】]", RegexOptions.IgnoreCase)]
    private static partial Regex NoiseSuffix();

    [GeneratedRegex(@"\s*\|.*$")]
    private static partial Regex PipeTail();

    // Keterangan versi di akhir judul: "(Alternate Version)", "[Remastered 2011]", "- Live".
    [GeneratedRegex(@"(\s*[\(\[][^\)\]]*[\)\]])+\s*$|\s+-\s+[^-]*\b(version|versi|remaster(ed)?|live|edit|mix|remix|acoustic|akustik)\b.*$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionTail();

    [GeneratedRegex(@"\s*(ft\.?|feat\.?)\s+.*$", RegexOptions.IgnoreCase)]
    private static partial Regex Featuring();

    [GeneratedRegex(@"\s*-\s*Topic$", RegexOptions.IgnoreCase)]
    private static partial Regex TopicChannel();

    [GeneratedRegex(@"\s+(VEVO|Official)$", RegexOptions.IgnoreCase)]
    private static partial Regex ChannelSuffix();

    /// <summary>
    /// Kandidat kueri dari yang paling mirip aslinya sampai yang paling dibersihkan.
    /// Duplikat dibuang.
    /// </summary>
    public static IReadOnlyList<TrackQuery> Build(string title, string artist, string album, TimeSpan duration)
    {
        var results = new List<TrackQuery>();
        void Add(string t, string a)
        {
            t = t.Trim();
            a = a.Trim();
            if (t.Length == 0) return;
            var q = new TrackQuery(t, a, album, duration);
            if (!results.Any(r => r.Title.Equals(q.Title, StringComparison.OrdinalIgnoreCase)
                                  && r.Artist.Equals(q.Artist, StringComparison.OrdinalIgnoreCase)))
                results.Add(q);
        }

        var cleanArtist = ChannelSuffix().Replace(TopicChannel().Replace(artist, ""), "");
        Add(title, cleanArtist);

        var cleanTitle = PipeTail().Replace(NoiseSuffix().Replace(title, ""), "");
        Add(cleanTitle, cleanArtist);

        // "Artis - Judul" di judul video: artis sebenarnya ada di judul, bukan nama kanal.
        int dash = cleanTitle.IndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0)
        {
            var left = cleanTitle[..dash];
            var right = cleanTitle[(dash + 3)..];
            Add(VersionTail().Replace(right, ""), left);
            Add(Featuring().Replace(VersionTail().Replace(right, ""), ""), Featuring().Replace(left, ""));
        }

        // Judul dasar tanpa keterangan versi: lirik versi lain biasanya sama.
        Add(VersionTail().Replace(cleanTitle, ""), cleanArtist);
        Add(Featuring().Replace(VersionTail().Replace(cleanTitle, ""), ""), Featuring().Replace(cleanArtist, ""));
        return results;
    }
}
