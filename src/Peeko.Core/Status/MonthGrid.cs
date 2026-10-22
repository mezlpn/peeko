// Diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): tampilan bulan di mode Calendar,
// tinggi mengikuti jumlah minggu di bulan itu.

namespace Peeko.Core.Status;

/// <summary>Satu sel tanggal; <see cref="Day"/> 0 berarti sel kosong sebelum tanggal 1.</summary>
public readonly record struct DayCell(int Day, bool IsToday, bool IsSunday);

/// <summary>Susunan kalender satu bulan, minggu dimulai hari Minggu seperti kalender dinding Indonesia.</summary>
public static class MonthGrid
{
    public static readonly string[] WeekdayInitials = ["M", "S", "S", "R", "K", "J", "S"];

    private static readonly string[] MonthNames =
        ["Januari", "Februari", "Maret", "April", "Mei", "Juni", "Juli", "Agustus", "September", "Oktober", "November", "Desember"];

    public static string MonthName(int month) => MonthNames[month - 1];

    /// <summary>Baris-baris minggu (4–6), tiap baris 7 sel.</summary>
    public static IReadOnlyList<DayCell[]> Build(DateOnly today) => Build(today.Year, today.Month, today);

    public static IReadOnlyList<DayCell[]> Build(int year, int month, DateOnly today)
    {
        int lead = (int)new DateOnly(year, month, 1).DayOfWeek; // Minggu = 0
        int days = DateTime.DaysInMonth(year, month);
        int rows = (lead + days + 6) / 7;

        var weeks = new List<DayCell[]>(rows);
        for (int r = 0; r < rows; r++)
        {
            var week = new DayCell[7];
            for (int c = 0; c < 7; c++)
            {
                int day = r * 7 + c - lead + 1;
                bool real = day >= 1 && day <= days;
                week[c] = new DayCell(
                    real ? day : 0,
                    real && today == new DateOnly(year, month, day),
                    c == 0);
            }
            weeks.Add(week);
        }
        return weeks;
    }
}
