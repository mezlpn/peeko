namespace Peeko.Core.Status;

/// <summary>Berapa bagian hari, minggu (mulai Senin), bulan, dan tahun ini yang sudah lewat (0–1).</summary>
public readonly record struct DayProgress(double Day, double Week, double Month, double Year)
{
    public static DayProgress At(DateTime now)
    {
        var today = now.Date;
        double day = (now - today).TotalDays;

        int sinceMonday = ((int)today.DayOfWeek + 6) % 7;
        double week = (sinceMonday + day) / 7;

        var monthStart = new DateTime(now.Year, now.Month, 1);
        double month = (now - monthStart).TotalDays / DateTime.DaysInMonth(now.Year, now.Month);

        var yearStart = new DateTime(now.Year, 1, 1);
        double year = (now - yearStart).TotalDays / (DateTime.IsLeapYear(now.Year) ? 366 : 365);

        return new DayProgress(day, week, month, year);
    }

    /// <summary>Persen bulat ke bawah, supaya 99,6% tidak tampil 100% sebelum waktunya.</summary>
    public static int Percent(double fraction) => (int)Math.Floor(Math.Clamp(fraction, 0, 1) * 100);
}
