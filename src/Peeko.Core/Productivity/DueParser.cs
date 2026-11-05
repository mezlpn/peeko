using System.Globalization;
using System.Text.RegularExpressions;

namespace Peeko.Core.Productivity;

public enum TaskRepeat { None, Daily, Weekdays, Weekly, Monthly }

/// <summary>Hasil membaca kalimat tugas: teks bersih, tenggat (bila ada), dan pengulangan.</summary>
public sealed record ParsedTask(string Text, DateTime? Due, bool HasTime, TaskRepeat Repeat);

/// <summary>
/// Membaca tenggat dari kalimat sehari-hari, bahasa Indonesia atau Inggris:
/// "besok jam 7 kumpul laporan", "telepon ibu jam 3 sore", "30 menit lagi angkat jemuran",
/// "tiap senin jam 6 olahraga", "rapat tgl 12 jam 9.30", "call mom tomorrow at 7pm".
/// Bagian waktu dibuang dari teks tugas. Tanpa jam: tenggatnya sepanjang hari itu (HasTime = false).
/// </summary>
public static partial class DueParser
{
    private static readonly string[] IdDays = ["minggu", "senin", "selasa", "rabu", "kamis", "jumat", "sabtu"];
    private static readonly string[] EnDays = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];
    private static readonly string[] Months =
        ["jan", "feb", "mar", "apr", "mei|may", "jun", "jul", "agu|agt|aug", "sep", "okt|oct", "nov", "des|dec"];

    private const string Day = "minggu|senin|selasa|rabu|kamis|jum'?at|sabtu|sunday|monday|tuesday|wednesday|thursday|friday|saturday";
    private const string Part = "pagi|siang|sore|malam|morning|afternoon|evening|night|am|pm";

    public static ParsedTask Parse(string input, DateTime now)
    {
        string text = " " + input.Trim() + " ";
        DateTime? date = null;
        TimeSpan? time = null;
        var repeat = TaskRepeat.None;
        DayOfWeek? weekday = null;
        string? partOfDay = null;

        string Take(Regex regex, Func<Match, bool> use)
        {
            var m = regex.Match(text);
            if (m.Success && use(m)) text = text.Remove(m.Index, m.Length).Insert(m.Index, " ");
            return text;
        }

        // ── Pengulangan ──
        Take(RepeatRx(), m =>
        {
            string what = m.Groups["what"].Value.ToLowerInvariant();
            if (what is "hari kerja" or "weekday" or "weekdays") repeat = TaskRepeat.Weekdays;
            else if (what is "hari" or "day" or "daily") repeat = TaskRepeat.Daily;
            else if (what is "bulan" or "month") repeat = TaskRepeat.Monthly;
            else if (what is "minggu" && !m.Groups["hari"].Success || what is "week") repeat = TaskRepeat.Weekly;
            else if (DayIndex(what) is { } d)
            {
                repeat = TaskRepeat.Weekly;
                weekday = d;
            }
            else return false;
            return true;
        });
        if (repeat == TaskRepeat.None && DailyRx().IsMatch(text))
        {
            repeat = TaskRepeat.Daily;
            text = DailyRx().Replace(text, " ", 1);
        }

        // ── Relatif: "30 menit lagi", "dalam 2 jam", "in 10 minutes" ──
        Take(RelativeRx(), m =>
        {
            int n = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
            string unit = m.Groups["u"].Value.ToLowerInvariant();
            if (unit is "minggu" || unit.StartsWith("week"))
            {
                date = now.Date.AddDays(7 * n);
                return n > 0;
            }
            if (unit is "hari" || unit.StartsWith("day"))
            {
                date = now.Date.AddDays(n);
                return n > 0;
            }
            var at = unit is "jam" || unit.StartsWith('h') ? now.AddHours(n) : now.AddMinutes(n);
            date = at.Date;
            time = new TimeSpan(at.Hour, at.Minute, 0);
            return n > 0;
        });

        // ── Jam: "jam 7", "pukul 19.30", "jam 3 sore", "7pm", "at 7:15", "19:00" ──
        if (time is null)
        {
            // Angka biasa ("beli 2 roti") bukan jam: harus ada "jam/pukul/at", menit, atau pagi/sore/pm.
            foreach (Match m in TimeRx().Matches(text))
            {
                int h = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
                int min = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
                bool marked = m.Groups["kw"].Success || m.Groups["m"].Success || m.Groups["p"].Success;
                if (!marked || h > 23 || min > 59) continue;
                partOfDay = m.Groups["p"].Success ? m.Groups["p"].Value.ToLowerInvariant() : null;
                time = new TimeSpan(ApplyPart(h, partOfDay), min, 0);
                text = text.Remove(m.Index, m.Length).Insert(m.Index, " ");
                break;
            }
        }

        // ── Hari: hari ini, besok, lusa, nanti malam, senin (depan), tomorrow, next friday ──
        Take(RelativeDayRx(), m =>
        {
            string w = m.Groups["w"].Value.ToLowerInvariant();
            date = w switch
            {
                "besok" or "tomorrow" => now.Date.AddDays(1),
                "lusa" => now.Date.AddDays(2),
                _ => now.Date,
            };
            if (m.Groups["p"].Success && time is null) time = DefaultTime(m.Groups["p"].Value.ToLowerInvariant());
            if (w is "tonight" or "nanti malam" && time is null) time = TimeSpan.FromHours(19);
            return true;
        });
        if (weekday is null)
        {
            Take(WeekdayRx(), m =>
            {
                weekday = DayIndex(m.Groups["d"].Value.ToLowerInvariant());
                return weekday is not null;
            });
        }

        // ── Tanggal: "tgl 12", "12/11", "12 nov", "12 november 2026" ──
        Take(DateRx(), m =>
        {
            int d = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture);
            int month = m.Groups["mo"].Success ? int.Parse(m.Groups["mo"].Value, CultureInfo.InvariantCulture)
                : m.Groups["mn"].Success ? MonthIndex(m.Groups["mn"].Value) : now.Month;
            int year = m.Groups["y"].Success ? int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture) : now.Year;
            if (year < 100) year += 2000;
            if (month is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(year, month)) return false;
            var candidate = new DateTime(year, month, d);
            // Tanpa tahun dan sudah lewat: maksudnya tahun/bulan depan.
            if (candidate < now.Date && !m.Groups["y"].Success)
                candidate = m.Groups["mo"].Success || m.Groups["mn"].Success ? candidate.AddYears(1) : candidate.AddMonths(1);
            date = candidate;
            return true;
        });

        if (weekday is { } wd && date is null)
        {
            int ahead = ((int)wd - (int)now.DayOfWeek + 7) % 7;
            var day = now.Date.AddDays(ahead);
            if (ahead == 0 && (time is null ? false : day + time.Value <= now)) day = day.AddDays(7);
            date = day;
        }

        if (repeat != TaskRepeat.None && date is null)
        {
            // "tiap hari jam 6": kejadian berikutnya.
            date = now.Date;
            if (repeat == TaskRepeat.Weekdays) while (date.Value.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.Value.AddDays(1);
        }

        if (time is { } t && date is null)
        {
            // Hanya jam: hari ini bila belum lewat. "jam 3" tanpa pagi/sore yang sudah lewat → 15.00 bila belum lewat.
            var today = now.Date + t;
            if (today > now) date = now.Date;
            else if (partOfDay is null && t.Hours < 12 && now.Date + t.Add(TimeSpan.FromHours(12)) > now)
            {
                time = t.Add(TimeSpan.FromHours(12));
                date = now.Date;
            }
            else date = now.Date.AddDays(1);
        }
        else if (time is { } t2 && date is { } dd && dd + t2 <= now && repeat != TaskRepeat.None)
        {
            date = Advance(dd + t2, repeat).Date;
        }

        string clean = Cleanup(text);
        if (clean.Length == 0) clean = input.Trim();
        DateTime? due = date is { } final ? final + (time ?? TimeSpan.Zero) : null;
        return new ParsedTask(clean, due, due is not null && time is not null, repeat);
    }

    /// <summary>Kejadian berikutnya setelah <paramref name="from"/> untuk tugas berulang.</summary>
    public static DateTime Advance(DateTime from, TaskRepeat repeat)
    {
        switch (repeat)
        {
            case TaskRepeat.Daily: return from.AddDays(1);
            case TaskRepeat.Weekly: return from.AddDays(7);
            case TaskRepeat.Monthly: return from.AddMonths(1);
            case TaskRepeat.Weekdays:
                var next = from.AddDays(1);
                while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) next = next.AddDays(1);
                return next;
            default: return from;
        }
    }

    /// <summary>Label singkat tenggat: "14.30", "Besok 07.00", "Sen 06.00", "12 Nov".</summary>
    public static string Describe(DateTime due, bool hasTime, DateTime now)
    {
        var culture = CultureInfo.GetCultureInfo("id-ID");
        string time = hasTime ? due.ToString("HH.mm", CultureInfo.InvariantCulture) : "";
        int days = (due.Date - now.Date).Days;
        string day = days switch
        {
            0 => hasTime ? "" : "Hari ini",
            1 => "Besok",
            -1 => "Kemarin",
            > 1 and < 7 => due.ToString("ddd", culture),
            _ => due.ToString("d MMM", culture),
        };
        return $"{day} {time}".Trim();
    }

    private static int ApplyPart(int h, string? part) => part switch
    {
        "sore" or "afternoon" or "pm" or "evening" when h < 12 => h + 12,
        "malam" or "night" when h is >= 6 and < 12 => h + 12,
        "siang" when h is >= 1 and < 6 => h + 12,
        "am" or "pagi" or "morning" when h == 12 => 0,
        _ => h,
    };

    private static TimeSpan DefaultTime(string part) => part switch
    {
        "pagi" or "morning" => TimeSpan.FromHours(7),
        "siang" or "afternoon" => TimeSpan.FromHours(12),
        "sore" => TimeSpan.FromHours(16),
        _ => TimeSpan.FromHours(19),
    };

    private static DayOfWeek? DayIndex(string word)
    {
        word = word.Replace("'", "");
        int i = Array.IndexOf(IdDays, word);
        if (i < 0) i = Array.IndexOf(EnDays, word);
        return i < 0 ? null : (DayOfWeek)i;
    }

    private static int MonthIndex(string name)
    {
        name = name.ToLowerInvariant();
        for (int i = 0; i < Months.Length; i++)
            if (Months[i].Split('|').Any(p => name.StartsWith(p, StringComparison.Ordinal))) return i + 1;
        return 0;
    }

    private static string Cleanup(string text)
    {
        text = SpacesRx().Replace(text, " ").Trim();
        // Sisa kata sambung di ujung: "kumpul laporan pada", "on", "at".
        text = EdgeWordsRx().Replace(text, "").Trim();
        return text.Trim(' ', ',', '-', '·', ':');
    }

    [GeneratedRegex(@"\b(?:tiap|setiap|every)\s+(?<hari>hari\s+)?(?<what>hari kerja|weekdays?|hari|day|minggu|week|bulan|month|" + Day + @")\b", RegexOptions.IgnoreCase)]
    private static partial Regex RepeatRx();

    [GeneratedRegex(@"\b(?:daily|harian)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DailyRx();

    // Durasi hanya dianggap tenggat bila jelas relatif ("dalam 2 jam", "30 menit lagi"), supaya "rapat 2 jam" tetap teks.
    [GeneratedRegex(@"\b(?:(?:dalam|in)\s+(?<n>\d{1,3})\s*(?<u>menit|mnt|minutes?|mins?|jam|hours?|hrs?|hari|days?|minggu|weeks?)|(?<n>\d{1,3})\s*(?<u>menit|mnt|minutes?|mins?|jam|hours?|hrs?|hari|days?|minggu|weeks?)\s+(?:lagi|from now))\b", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeRx();

    [GeneratedRegex(@"(?<!\d)(?:\b(?<kw>jam|pukul|pk\.?|at)\s*|(?<kw>@)\s*)?(?<h>\d{1,2})(?:[.:](?<m>\d{2}))?\s*(?<p>" + Part + @")?(?![\d/])", RegexOptions.IgnoreCase)]
    private static partial Regex TimeRx();

    [GeneratedRegex(@"\b(?<w>hari ini|today|besok|tomorrow|lusa|nanti malam|tonight|nanti)(?:\s+(?<p>pagi|siang|sore|malam|morning|afternoon|evening|night))?\b", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeDayRx();

    [GeneratedRegex(@"\b(?:(?:hari|next|on)\s+)?(?<d>" + Day + @")(?:\s+(?:depan|ini))?\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeekdayRx();

    [GeneratedRegex(@"\b(?:(?:tgl\.?|tanggal|on)\s*(?<d>\d{1,2})(?:\s*/\s*(?<mo>\d{1,2})(?:\s*/\s*(?<y>\d{2,4}))?)?|(?<d>\d{1,2})\s*/\s*(?<mo>\d{1,2})(?:\s*/\s*(?<y>\d{2,4}))?|(?<d>\d{1,2})\s+(?<mn>jan\w*|feb\w*|mar\w*|apr\w*|mei|may|jun\w*|jul\w*|agu\w*|agt|aug\w*|sep\w*|okt\w*|oct\w*|nov\w*|des\w*|dec\w*)(?:\s+(?<y>\d{4}))?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DateRx();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacesRx();

    [GeneratedRegex(@"^(?:pada|on|at|di|untuk)\s+|\s+(?:pada|on|at|di|untuk|yang|nanti)$", RegexOptions.IgnoreCase)]
    private static partial Regex EdgeWordsRx();
}
