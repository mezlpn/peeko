using Peeko.Core.Status;

namespace Peeko.Core.Tests;

public class ICalendarTests
{
    // Tes dihitung dalam waktu lokal mesin; acara memakai TZID/floating supaya hasilnya tetap.
    private const string Ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
        "BEGIN:VEVENT\r\nDTSTART;TZID=Asia/Jakarta:20261109T090000\r\nDTEND;TZID=Asia/Jakarta:20261109T100000\r\n" +
        "RRULE:FREQ=WEEKLY;BYDAY=MO,WE;COUNT=4\r\nEXDATE;TZID=Asia/Jakarta:20261111T090000\r\n" +
        "SUMMARY:Rapat tim\\, mingguan\r\nDESCRIPTION:Gabung: https://meet.google.com/abc-defg-hij\\nSampai jumpa\r\n" +
        "BEGIN:VALARM\r\nACTION:DISPLAY\r\nSUMMARY:Bukan acara\r\nEND:VALARM\r\nEND:VEVENT\r\n" +
        "BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20261110\r\nDTEND;VALUE=DATE:20261111\r\nSUMMARY:Libur\r\nEND:VEVENT\r\n" +
        "BEGIN:VEVENT\r\nDTSTART:20261110T130000\r\nDURATION:PT1H30M\r\nSUMMARY:Kuliah panjang yang judulnya\r\n  dilipat\r\n" +
        "LOCATION:https://us02web.zoom.us/j/123?pwd=x\r\nEND:VEVENT\r\n" +
        "BEGIN:VEVENT\r\nDTSTART:20261110T150000\r\nSTATUS:CANCELLED\r\nSUMMARY:Batal\r\nEND:VEVENT\r\n" +
        "END:VCALENDAR\r\n";

    private static DateTime Jakarta(int y, int mo, int d, int h, int mi) =>
        TimeZoneInfo.ConvertTime(new DateTime(y, mo, d, h, mi, 0), TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta"), TimeZoneInfo.Local);

    [Fact]
    public void Expands_weekly_rule_with_exdate_and_reads_fields()
    {
        var events = ICalendar.Parse(Ics, new DateTime(2026, 11, 1), new DateTime(2026, 12, 1));
        var meetings = events.Where(e => e.Title == "Rapat tim, mingguan").ToList();
        Assert.Equal([Jakarta(2026, 11, 9, 9, 0), Jakarta(2026, 11, 16, 9, 0), Jakarta(2026, 11, 18, 9, 0)], meetings.Select(m => m.Start));
        Assert.Equal("https://meet.google.com/abc-defg-hij", meetings[0].JoinUrl);
        Assert.Equal(TimeSpan.FromHours(1), meetings[0].End - meetings[0].Start);
        Assert.DoesNotContain(events, e => e.Title is "Bukan acara" or "Batal");
    }

    [Fact]
    public void Reads_all_day_duration_folded_lines_and_zoom_links()
    {
        var events = ICalendar.Parse(Ics, new DateTime(2026, 11, 10), new DateTime(2026, 11, 11));
        var holiday = events.Single(e => e.Title == "Libur");
        Assert.True(holiday.AllDay);
        var lecture = events.Single(e => e.Title.StartsWith("Kuliah"));
        Assert.Equal("Kuliah panjang yang judulnya dilipat", lecture.Title);
        Assert.Equal(new DateTime(2026, 11, 10, 14, 30, 0), lecture.End);
        Assert.Equal("https://us02web.zoom.us/j/123?pwd=x", lecture.JoinUrl);
    }
}
