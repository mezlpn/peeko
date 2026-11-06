using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Peeko.Core.Status;

/// <summary>Satu kejadian acara (acara berulang sudah dipecah per tanggal).</summary>
public sealed record CalendarEvent(string Title, DateTime Start, DateTime End, bool AllDay, string? Location, string? JoinUrl)
{
    public bool IsNow(DateTime now) => now >= Start && now < End;
}

/// <summary>
/// Pembaca iCalendar (.ics) yang cukup untuk kalender Google/Outlook: VEVENT dengan DTSTART/DTEND,
/// SUMMARY, LOCATION, DESCRIPTION, zona waktu UTC/TZID/floating, dan RRULE DAILY/WEEKLY/MONTHLY
/// (INTERVAL, COUNT, UNTIL, BYDAY) serta EXDATE. Semua waktu dikembalikan dalam waktu lokal.
/// </summary>
public static partial class ICalendar
{
    private const int MaxOccurrences = 2000;

    public static IReadOnlyList<CalendarEvent> Parse(string ics, DateTime from, DateTime to)
    {
        var result = new List<CalendarEvent>();
        var lines = Unfold(ics);
        Dictionary<string, (string Value, Dictionary<string, string> Params)>? ev = null;
        var exdates = new List<(string, Dictionary<string, string>)>();
        int depth = 0;
        foreach (var line in lines)
        {
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                ev = new(StringComparer.OrdinalIgnoreCase);
                exdates.Clear();
                continue;
            }
            if (ev is not null && line.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase))
            {
                depth++; // VALARM di dalam acara: abaikan isinya
                continue;
            }
            if (ev is not null && depth > 0)
            {
                if (line.StartsWith("END:", StringComparison.OrdinalIgnoreCase)) depth--;
                continue;
            }
            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (ev is not null) Expand(ev, exdates, from, to, result);
                ev = null;
                continue;
            }
            if (ev is null) continue;
            var (name, prms, value) = SplitLine(line);
            if (name.Equals("EXDATE", StringComparison.OrdinalIgnoreCase)) exdates.Add((value, prms));
            else ev[name] = (value, prms);
        }
        return result.OrderBy(e => e.Start).ThenBy(e => e.Title).ToList();
    }

    private static void Expand(Dictionary<string, (string Value, Dictionary<string, string> Params)> ev,
        List<(string, Dictionary<string, string>)> exdates, DateTime from, DateTime to, List<CalendarEvent> into)
    {
        if (!ev.TryGetValue("DTSTART", out var startRaw)) return;
        if (ev.TryGetValue("STATUS", out var status) && status.Value.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)) return;
        if (ParseTime(startRaw.Value, startRaw.Params) is not { } start) return;
        bool allDay = startRaw.Params.TryGetValue("VALUE", out var vt) && vt.Equals("DATE", StringComparison.OrdinalIgnoreCase)
                      || startRaw.Value.Length == 8;
        TimeSpan duration;
        if (ev.TryGetValue("DTEND", out var endRaw) && ParseTime(endRaw.Value, endRaw.Params) is { } end && end > start) duration = end - start;
        else if (ev.TryGetValue("DURATION", out var dur) && ParseDuration(dur.Value) is { } d) duration = d;
        else duration = allDay ? TimeSpan.FromDays(1) : TimeSpan.Zero;

        string title = ev.TryGetValue("SUMMARY", out var s) ? Unescape(s.Value) : "(Tanpa judul)";
        string? location = ev.TryGetValue("LOCATION", out var l) && l.Value.Length > 0 ? Unescape(l.Value) : null;
        string? description = ev.TryGetValue("DESCRIPTION", out var desc) ? Unescape(desc.Value) : null;
        string? join = FindJoinUrl(location) ?? FindJoinUrl(description) ?? (ev.TryGetValue("URL", out var u) ? FindJoinUrl(u.Value) : null);

        var excluded = new HashSet<DateTime>(exdates.SelectMany(x => x.Item1.Split(','))
            .Select(v => ParseTime(v, exdates.First().Item2)).Where(t => t is not null).Select(t => t!.Value));

        void Add(DateTime occurrence)
        {
            if (excluded.Contains(occurrence)) return;
            var e = occurrence + duration;
            if (e <= from && !(duration == TimeSpan.Zero && occurrence >= from) || occurrence >= to) return;
            into.Add(new CalendarEvent(title, occurrence, e, allDay, location, join));
        }

        if (!ev.TryGetValue("RRULE", out var rule))
        {
            Add(start);
            return;
        }
        foreach (var occurrence in Recur(start, rule.Value, to)) Add(occurrence);
    }

    private static IEnumerable<DateTime> Recur(DateTime start, string rule, DateTime to)
    {
        var parts = rule.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
            .ToDictionary(p => p[0].ToUpperInvariant(), p => p[1], StringComparer.OrdinalIgnoreCase);
        var freq = parts.GetValueOrDefault("FREQ", "").ToUpperInvariant();
        int interval = int.TryParse(parts.GetValueOrDefault("INTERVAL"), out var i) && i > 0 ? i : 1;
        int? count = int.TryParse(parts.GetValueOrDefault("COUNT"), out var c) ? c : null;
        DateTime? until = parts.TryGetValue("UNTIL", out var u) ? ParseTime(u, []) : null;
        var byDay = parts.TryGetValue("BYDAY", out var bd)
            ? bd.Split(',').Select(d => DayOf(d[^2..])).Where(d => d is not null).Select(d => d!.Value).ToHashSet()
            : null;
        var limit = until is { } un && un < to ? un : to;

        int produced = 0;
        bool Emit(DateTime t, out bool stop)
        {
            stop = t > limit || (count is { } n && produced >= n) || produced >= MaxOccurrences;
            if (stop) return false;
            produced++;
            return true;
        }

        switch (freq)
        {
            case "DAILY":
                for (var t = start; ; t = t.AddDays(interval))
                {
                    if (byDay is not null && !byDay.Contains(t.DayOfWeek)) { if (t > limit) yield break; continue; }
                    if (Emit(t, out var stop)) yield return t;
                    if (stop) yield break;
                }
            case "WEEKLY":
                var days = byDay ?? [start.DayOfWeek];
                var weekStart = start.Date.AddDays(-(((int)start.DayOfWeek + 6) % 7)); // Senin
                for (var w = weekStart; ; w = w.AddDays(7 * interval))
                {
                    for (int k = 0; k < 7; k++)
                    {
                        var day = w.AddDays(k);
                        if (!days.Contains(day.DayOfWeek)) continue;
                        var t = day + start.TimeOfDay;
                        if (t < start) continue;
                        if (Emit(t, out var stop)) yield return t;
                        if (stop) yield break;
                    }
                }
            case "MONTHLY":
                for (int m = 0; ; m += interval)
                {
                    var t = start.AddMonths(m);
                    if (t.Day != start.Day) continue; // 31 di bulan pendek dilewati, sesuai RFC 5545
                    if (Emit(t, out var stop)) yield return t;
                    if (stop) yield break;
                }
            case "YEARLY":
                for (int y = 0; ; y += interval)
                {
                    var t = start.AddYears(y);
                    if (Emit(t, out var stop)) yield return t;
                    if (stop) yield break;
                }
            default:
                yield return start;
                yield break;
        }
    }

    private static DayOfWeek? DayOf(string code) => code.ToUpperInvariant() switch
    {
        "MO" => DayOfWeek.Monday,
        "TU" => DayOfWeek.Tuesday,
        "WE" => DayOfWeek.Wednesday,
        "TH" => DayOfWeek.Thursday,
        "FR" => DayOfWeek.Friday,
        "SA" => DayOfWeek.Saturday,
        "SU" => DayOfWeek.Sunday,
        _ => null,
    };

    /// <summary>"20261107T090000Z" (UTC), "20261107T090000" + TZID, atau "20261107" (sepanjang hari).</summary>
    internal static DateTime? ParseTime(string value, Dictionary<string, string> prms)
    {
        value = value.Trim();
        if (value.Length == 8 && DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        bool utc = value.EndsWith('Z');
        var core = utc ? value[..^1] : value;
        if (!DateTime.TryParseExact(core, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return null;
        if (utc) return DateTime.SpecifyKind(t, DateTimeKind.Utc).ToLocalTime();
        if (prms.TryGetValue("TZID", out var tzid) && FindZone(tzid) is { } zone)
        {
            try
            {
                return TimeZoneInfo.ConvertTime(t, zone, TimeZoneInfo.Local);
            }
            catch (ArgumentException)
            {
            }
        }
        return t; // "floating": jam setempat
    }

    private static TimeZoneInfo? FindZone(string id)
    {
        id = id.Trim('"');
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id); // .NET 8 menerjemahkan nama IANA (Asia/Jakarta) di Windows
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    internal static TimeSpan? ParseDuration(string value)
    {
        var m = DurationRegex().Match(value.Trim());
        if (!m.Success) return null;
        int G(string n) => m.Groups[n].Success ? int.Parse(m.Groups[n].Value, CultureInfo.InvariantCulture) : 0;
        return new TimeSpan(G("w") * 7 + G("d"), G("h"), G("m"), G("s"));
    }

    [GeneratedRegex(@"^P(?:(?<w>\d+)W)?(?:(?<d>\d+)D)?(?:T(?:(?<h>\d+)H)?(?:(?<m>\d+)M)?(?:(?<s>\d+)S)?)?$")]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"https://[^\s<>""\\]*(?:meet\.google\.com|zoom\.us|teams\.microsoft\.com|teams\.live\.com|webex\.com)[^\s<>""\\]*", RegexOptions.IgnoreCase)]
    private static partial Regex JoinRegex();

    /// <summary>Tautan rapat (Meet, Zoom, Teams, Webex) di lokasi atau keterangan acara.</summary>
    public static string? FindJoinUrl(string? text) =>
        text is not null && JoinRegex().Match(text) is { Success: true } m ? m.Value.TrimEnd('.', ',', ')', '>') : null;

    private static List<string> Unfold(string ics)
    {
        var lines = new List<string>();
        foreach (var raw in ics.Replace("\r\n", "\n").Split('\n'))
        {
            if ((raw.StartsWith(' ') || raw.StartsWith('\t')) && lines.Count > 0) lines[^1] += raw[1..];
            else if (raw.Length > 0) lines.Add(raw);
        }
        return lines;
    }

    private static (string Name, Dictionary<string, string> Params, string Value) SplitLine(string line)
    {
        // Titik dua pertama di luar tanda kutip memisahkan nama+parameter dan nilai.
        bool quoted = false;
        int colon = -1;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') quoted = !quoted;
            else if (line[i] == ':' && !quoted) { colon = i; break; }
        }
        if (colon < 0) return (line, [], "");
        var head = line[..colon].Split(';');
        var prms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in head.Skip(1))
        {
            int eq = p.IndexOf('=');
            if (eq > 0) prms[p[..eq]] = p[(eq + 1)..].Trim('"');
        }
        return (head[0], prms, line[(colon + 1)..]);
    }

    private static string Unescape(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                char n = value[++i];
                sb.Append(n is 'n' or 'N' ? '\n' : n);
            }
            else
            {
                sb.Append(value[i]);
            }
        }
        return sb.ToString().Trim();
    }
}
