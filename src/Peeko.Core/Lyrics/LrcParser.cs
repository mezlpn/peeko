using System.Globalization;
using System.Text.RegularExpressions;

namespace Peeko.Core.Lyrics;

public sealed record LyricLine(TimeSpan Time, string Text);

/// <summary>Lirik tersinkron yang sudah diurutkan berdasarkan waktu.</summary>
public sealed class SyncedLyrics
{
    public SyncedLyrics(IReadOnlyList<LyricLine> lines) => Lines = lines;

    public IReadOnlyList<LyricLine> Lines { get; }

    /// <summary>Indeks baris terakhir yang waktunya ≤ <paramref name="position"/>, atau −1 sebelum baris pertama.</summary>
    public int IndexAt(TimeSpan position)
    {
        int lo = 0, hi = Lines.Count - 1, found = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (Lines[mid].Time <= position)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return found;
    }

    /// <summary>Waktu baris berikutnya setelah <paramref name="index"/>, atau null jika sudah baris terakhir.</summary>
    public TimeSpan? NextTime(int index) => index + 1 < Lines.Count ? Lines[index + 1].Time : null;
}

/// <summary>
/// Parser format LRC: <c>[mm:ss.xx] teks</c>, beberapa timestamp per baris, dan tag <c>[offset:±ms]</c>.
/// Baris kosong (jeda instrumental) diganti "♪".
/// </summary>
public static partial class LrcParser
{
    public const string BreakSymbol = "♪";

    [GeneratedRegex(@"\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]")]
    private static partial Regex TimeTag();

    [GeneratedRegex(@"^\s*\[offset:\s*([+-]?\d+)\s*\]\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetTag();

    public static SyncedLyrics Parse(string lrc)
    {
        var lines = new List<LyricLine>();
        var offset = TimeSpan.Zero;

        foreach (var raw in lrc.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            var offsetMatch = OffsetTag().Match(line);
            if (offsetMatch.Success)
            {
                // Offset positif berarti lirik muncul lebih awal.
                offset = TimeSpan.FromMilliseconds(-int.Parse(offsetMatch.Groups[1].Value, CultureInfo.InvariantCulture));
                continue;
            }

            var times = new List<TimeSpan>();
            int pos = 0;
            Match m;
            while ((m = TimeTag().Match(line, pos)).Success && m.Index == pos)
            {
                times.Add(ParseTime(m));
                pos = m.Index + m.Length;
            }
            if (times.Count == 0) continue; // metadata ([ar:], [ti:]) atau baris tanpa waktu

            var text = line[pos..].Trim();
            if (text.Length == 0) text = BreakSymbol;
            foreach (var t in times)
                lines.Add(new LyricLine(t, text));
        }

        if (offset != TimeSpan.Zero)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i].Time + offset;
                lines[i] = lines[i] with { Time = t < TimeSpan.Zero ? TimeSpan.Zero : t };
            }
        }

        // Urutan stabil: baris dengan waktu sama tetap sesuai urutan di file.
        var sorted = lines.Select((l, i) => (l, i)).OrderBy(x => x.l.Time).ThenBy(x => x.i).Select(x => x.l).ToList();

        // Jeda "♪" berturut-turut atau di awal tidak berguna untuk ditampilkan.
        var cleaned = new List<LyricLine>(sorted.Count);
        foreach (var l in sorted)
        {
            bool isBreak = l.Text == BreakSymbol;
            if (isBreak && (cleaned.Count == 0 || cleaned[^1].Text == BreakSymbol)) continue;
            cleaned.Add(l);
        }
        while (cleaned.Count > 0 && cleaned[^1].Text == BreakSymbol) cleaned.RemoveAt(cleaned.Count - 1);

        return new SyncedLyrics(cleaned);
    }

    private static TimeSpan ParseTime(Match m)
    {
        int minutes = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        int seconds = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        int ms = 0;
        if (m.Groups[3].Success)
        {
            var frac = m.Groups[3].Value;
            ms = int.Parse(frac, CultureInfo.InvariantCulture) * frac.Length switch { 1 => 100, 2 => 10, _ => 1 };
        }
        return new TimeSpan(0, 0, minutes, seconds, ms);
    }
}
