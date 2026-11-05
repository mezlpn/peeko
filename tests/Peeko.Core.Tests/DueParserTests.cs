using Peeko.Core.Productivity;

namespace Peeko.Core.Tests;

public class DueParserTests
{
    // Kamis, 5 November 2026, 11.10
    private static readonly DateTime Now = new(2026, 11, 5, 11, 10, 0);

    private static ParsedTask P(string s) => DueParser.Parse(s, Now);

    [Theory]
    [InlineData("besok jam 7 kumpul laporan", "kumpul laporan", "2026-11-06 07:00")]
    [InlineData("telepon ibu jam 3 sore", "telepon ibu", "2026-11-05 15:00")]
    [InlineData("rapat pukul 19.30", "rapat", "2026-11-05 19:30")]
    [InlineData("30 menit lagi angkat jemuran", "angkat jemuran", "2026-11-05 11:40")]
    [InlineData("dalam 2 jam kirim email", "kirim email", "2026-11-05 13:10")]
    [InlineData("jumat jam 9 bimbingan", "bimbingan", "2026-11-06 09:00")]
    [InlineData("rapat tgl 12 jam 9.30", "rapat", "2026-11-12 09:30")]
    [InlineData("bayar listrik 20/11", "bayar listrik", "2026-11-20 00:00")]
    [InlineData("ulang tahun dina 3 des", "ulang tahun dina", "2026-12-03 00:00")]
    [InlineData("call mom tomorrow at 7pm", "call mom", "2026-11-06 19:00")]
    [InlineData("nanti malam nonton", "nonton", "2026-11-05 19:00")]
    [InlineData("besok pagi lari", "lari", "2026-11-06 07:00")]
    [InlineData("lusa jam 13:00 presentasi", "presentasi", "2026-11-07 13:00")]
    public void Reads_due(string input, string text, string due)
    {
        var p = P(input);
        Assert.Equal(text, p.Text);
        Assert.Equal(DateTime.Parse(due), p.Due);
    }

    [Fact]
    public void Plain_numbers_and_durations_stay_in_the_text()
    {
        var p = P("beli 2 roti");
        Assert.Equal("beli 2 roti", p.Text);
        Assert.Null(p.Due);

        p = P("rapat 2 jam");
        Assert.Equal("rapat 2 jam", p.Text);
        Assert.Null(p.Due);
    }

    [Fact]
    public void Bare_hour_already_past_means_this_afternoon()
    {
        // 11.10: "jam 3" = 15.00 hari ini, "jam 11" sudah lewat → 23.00.
        Assert.Equal(new DateTime(2026, 11, 5, 15, 0, 0), P("jemput adik jam 3").Due);
        Assert.Equal(new DateTime(2026, 11, 5, 23, 0, 0), P("matikan kompor jam 11").Due);
        // "jam 9 pagi" sudah lewat → besok.
        Assert.Equal(new DateTime(2026, 11, 6, 9, 0, 0), P("absen jam 9 pagi").Due);
    }

    [Fact]
    public void Date_only_has_no_time()
    {
        var p = P("besok kumpul tugas");
        Assert.Equal(new DateTime(2026, 11, 6), p.Due);
        Assert.False(p.HasTime);
    }

    [Theory]
    [InlineData("tiap senin jam 6 olahraga", "olahraga", TaskRepeat.Weekly, "2026-11-09 06:00")]
    [InlineData("setiap hari jam 21 minum vitamin", "minum vitamin", TaskRepeat.Daily, "2026-11-05 21:00")]
    [InlineData("tiap hari jam 7 sarapan", "sarapan", TaskRepeat.Daily, "2026-11-06 07:00")]
    [InlineData("every day at 8pm journal", "journal", TaskRepeat.Daily, "2026-11-05 20:00")]
    [InlineData("tiap bulan tgl 25 bayar kos", "bayar kos", TaskRepeat.Monthly, "2026-11-25 00:00")]
    [InlineData("tiap hari kerja jam 8 standup", "standup", TaskRepeat.Weekdays, "2026-11-06 08:00")]
    [InlineData("tiap hari minggu jam 7 cuci motor", "cuci motor", TaskRepeat.Weekly, "2026-11-08 07:00")]
    public void Reads_repeats(string input, string text, TaskRepeat repeat, string due)
    {
        var p = P(input);
        Assert.Equal(text, p.Text);
        Assert.Equal(repeat, p.Repeat);
        Assert.Equal(DateTime.Parse(due), p.Due);
    }

    [Fact]
    public void Weeks_word_is_not_sunday()
    {
        var p = P("2 minggu lagi perpanjang SIM");
        Assert.Equal("perpanjang SIM", p.Text);
        Assert.Equal(new DateTime(2026, 11, 19), p.Due);
    }

    [Fact]
    public void Advance_skips_weekends_for_weekdays()
    {
        var friday = new DateTime(2026, 11, 6, 8, 0, 0);
        Assert.Equal(new DateTime(2026, 11, 9, 8, 0, 0), DueParser.Advance(friday, TaskRepeat.Weekdays));
        Assert.Equal(new DateTime(2026, 12, 6, 8, 0, 0), DueParser.Advance(friday, TaskRepeat.Monthly));
    }

    [Theory]
    [InlineData("2026-11-05 14:30", true, "14.30")]
    [InlineData("2026-11-06 07:00", true, "Besok 07.00")]
    [InlineData("2026-11-06 00:00", false, "Besok")]
    [InlineData("2026-11-05 00:00", false, "Hari ini")]
    [InlineData("2026-11-20 00:00", false, "20 Nov")]
    public void Describes_due(string due, bool hasTime, string expected) =>
        Assert.Equal(expected, DueParser.Describe(DateTime.Parse(due), hasTime, Now));
}
