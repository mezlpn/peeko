namespace Peeko.Core.Prayer;

/// <summary>Waktu dalam jadwal harian. Imsak dan Terbit hanya penanda, bukan waktu salat.</summary>
public enum PrayerName { Imsak, Subuh, Terbit, Dzuhur, Ashar, Maghrib, Isya }

public sealed record PrayerTime(PrayerName Name, DateTime At)
{
    /// <summary>Lima waktu salat wajib (diingatkan dan dihitung mundur).</summary>
    public bool IsPrayer => Name is not (PrayerName.Imsak or PrayerName.Terbit);

    public string Label => PrayerTimes.Label(Name);
}

/// <summary>
/// Jadwal salat dihitung sendiri (offline) dengan kriteria Kemenag RI: Subuh saat matahari −20°, Isya −18°,
/// Ashar bayangan = 1× tinggi benda (Syafi'i), Imsak 10 menit sebelum Subuh, ihtiyat +2 menit (Dzuhur +3,
/// Terbit −2) lalu dibulatkan ke menit berikutnya. Dibandingkan jadwal resmi Kemenag (api.myquran.com)
/// untuk Yogyakarta dan Jakarta, selisihnya 0–1 menit.
/// Rumus posisi matahari: algoritme ringkas U.S. Naval Observatory, seperti yang dipakai banyak kalkulator jadwal salat.
/// </summary>
public static class PrayerTimes
{
    public const double FajrAngle = 20, IshaAngle = 18;
    private const double DefaultElevation = 30;

    public static string Label(PrayerName name) => name switch
    {
        PrayerName.Imsak => "Imsak",
        PrayerName.Subuh => "Subuh",
        PrayerName.Terbit => "Terbit",
        PrayerName.Dzuhur => "Dzuhur",
        PrayerName.Ashar => "Ashar",
        PrayerName.Maghrib => "Maghrib",
        _ => "Isya",
    };

    /// <summary>Jadwal satu hari untuk lokasi itu. <paramref name="utcOffset"/> = zona waktu lokal tanggal tersebut.</summary>
    public static IReadOnlyList<PrayerTime> For(DateOnly date, double latitude, double longitude, TimeSpan utcOffset,
        double elevation = DefaultElevation)
    {
        double jd = Julian(date.Year, date.Month, date.Day) - longitude / (15 * 24);
        double riseSet = 0.833 + 0.0347 * Math.Sqrt(Math.Max(0, elevation));

        double Noon(double t) => Fix(12 - Sun(jd + t).Equation, 24);
        double AngleTime(double angle, double t, bool beforeNoon)
        {
            double decl = Sun(jd + t).Declination;
            double cosT = (-Sin(angle) - Sin(decl) * Sin(latitude)) / (Cos(decl) * Cos(latitude));
            // Di lintang sangat tinggi matahari bisa tidak mencapai sudut itu; jepit supaya tetap ada nilai.
            double h = Acos(Math.Clamp(cosT, -1, 1)) / 15;
            return Noon(t) + (beforeNoon ? -h : h);
        }
        double Asr(double t)
        {
            double decl = Sun(jd + t).Declination;
            return AngleTime(-Acot(1 + Tan(Math.Abs(latitude - decl))), t, beforeNoon: false);
        }

        // Dua putaran: putaran kedua memakai perkiraan jam dari putaran pertama.
        double[] t = [5, 6, 12, 13, 18, 18];
        for (int i = 0; i < 2; i++)
        {
            var p = t.Select(x => x / 24).ToArray();
            t = [AngleTime(FajrAngle, p[0], true), AngleTime(riseSet, p[1], true), Noon(p[2]), Asr(p[3]),
                AngleTime(riseSet, p[4], false), AngleTime(IshaAngle, p[5], false)];
        }

        // Lintang tinggi (musim panas Eropa): Subuh/Isya bisa tidak pernah tercapai. Aturan "angle based":
        // paling jauh (sudut/60) × panjang malam dari terbit/terbenam.
        double night = 24 - (t[4] - t[1]);
        double fajrLimit = t[1] - FajrAngle / 60 * night, ishaLimit = t[4] + IshaAngle / 60 * night;
        if (double.IsNaN(t[0]) || t[0] < fajrLimit) t[0] = fajrLimit;
        if (double.IsNaN(t[5]) || t[5] > ishaLimit) t[5] = ishaLimit;

        double zone = utcOffset.TotalHours - longitude / 15;
        double Minutes(double hours, double ihtiyat) => (hours + zone) * 60 + ihtiyat;
        var midnight = date.ToDateTime(TimeOnly.MinValue);
        DateTime Up(double minutes) => midnight.AddMinutes(Math.Ceiling(minutes - 1e-9));
        DateTime Down(double minutes) => midnight.AddMinutes(Math.Floor(minutes));

        double subuh = Minutes(t[0], 2);
        return
        [
            new(PrayerName.Imsak, Up(subuh - 10)),
            new(PrayerName.Subuh, Up(subuh)),
            new(PrayerName.Terbit, Down(Minutes(t[1], -2))),
            new(PrayerName.Dzuhur, Up(Minutes(t[2], 3))),
            new(PrayerName.Ashar, Up(Minutes(t[3], 2))),
            new(PrayerName.Maghrib, Up(Minutes(t[4], 2))),
            new(PrayerName.Isya, Up(Minutes(t[5], 2))),
        ];
    }

    /// <summary>Waktu salat wajib berikutnya setelah <paramref name="now"/> (bisa Subuh besok).</summary>
    public static PrayerTime Next(DateTime now, Func<DateOnly, IReadOnlyList<PrayerTime>> scheduleFor)
    {
        var today = DateOnly.FromDateTime(now);
        return scheduleFor(today).FirstOrDefault(p => p.IsPrayer && p.At > now)
            ?? scheduleFor(today.AddDays(1)).First(p => p.IsPrayer);
    }

    // ── Posisi matahari ────────────────────────────────────────────────────

    private static (double Declination, double Equation) Sun(double jd)
    {
        double d = jd - 2451545.0;
        double g = Fix(357.529 + 0.98560028 * d, 360);
        double q = Fix(280.459 + 0.98564736 * d, 360);
        double l = Fix(q + 1.915 * Sin(g) + 0.020 * Sin(2 * g), 360);
        double e = 23.439 - 0.00000036 * d;
        double ra = Atan2(Cos(e) * Sin(l), Cos(l)) / 15;
        return (Asin(Sin(e) * Sin(l)), q / 15 - Fix(ra, 24));
    }

    private static double Julian(int year, int month, int day)
    {
        if (month <= 2)
        {
            year--;
            month += 12;
        }
        int a = year / 100;
        int b = 2 - a + a / 4;
        return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;
    }

    private static double Fix(double a, double b)
    {
        a -= b * Math.Floor(a / b);
        return a < 0 ? a + b : a;
    }

    private static double Rad(double d) => d * Math.PI / 180;
    private static double Deg(double r) => r * 180 / Math.PI;
    private static double Sin(double d) => Math.Sin(Rad(d));
    private static double Cos(double d) => Math.Cos(Rad(d));
    private static double Tan(double d) => Math.Tan(Rad(d));
    private static double Asin(double x) => Deg(Math.Asin(x));
    private static double Acos(double x) => Deg(Math.Acos(x));
    private static double Atan2(double y, double x) => Deg(Math.Atan2(y, x));
    private static double Acot(double x) => Deg(Math.Atan(1 / x));
}
