using Peeko.Core.Status;

namespace Peeko.Core.Tests;

public class DayProgressTests
{
    [Fact]
    public void Noon_on_wednesday_mid_november()
    {
        // Rabu 11 Nov 2026 12:00
        var p = DayProgress.At(new DateTime(2026, 11, 11, 12, 0, 0));
        Assert.Equal(0.5, p.Day, 6);
        Assert.Equal((2 + 0.5) / 7, p.Week, 6);
        Assert.Equal(10.5 / 30, p.Month, 6);
        Assert.Equal(50, DayProgress.Percent(p.Day));
        Assert.Equal(86, DayProgress.Percent(p.Year));
    }

    [Fact]
    public void Week_starts_on_monday_and_percent_never_rounds_up()
    {
        Assert.Equal(0, DayProgress.At(new DateTime(2026, 11, 9)).Week, 6); // Senin
        Assert.Equal(6.0 / 7, DayProgress.At(new DateTime(2026, 11, 15)).Week, 6); // Minggu
        Assert.Equal(99, DayProgress.Percent(0.9999));
    }
}

public class ScreenTimeTests
{
    private static readonly DateTime T0 = new(2026, 11, 7, 9, 0, 0);

    [Fact]
    public void Counts_foreground_time_per_app_and_stops_on_pause()
    {
        var st = new ScreenTime();
        st.Switch("Chrome", T0);
        st.Switch("Code", T0.AddMinutes(30));
        st.Pause(T0.AddMinutes(50));
        st.Flush(T0.AddHours(2)); // jeda tidak dihitung
        st.Switch("Chrome", T0.AddHours(2));
        st.Flush(T0.AddHours(2).AddMinutes(10));

        var day = st.Day(T0);
        Assert.Equal("Chrome", day[0].App);
        Assert.Equal(TimeSpan.FromMinutes(40), day[0].Time);
        Assert.Equal(TimeSpan.FromMinutes(20), day[1].Time);
        Assert.Equal(TimeSpan.FromMinutes(60), st.Total(T0));
    }

    [Fact]
    public void Splits_at_midnight_and_keeps_two_weeks()
    {
        var st = new ScreenTime();
        var late = new DateTime(2026, 11, 7, 23, 50, 0);
        st.Switch("Code", late);
        st.Flush(late.AddMinutes(20));
        Assert.Equal(TimeSpan.FromMinutes(10), st.Total(late));
        Assert.Equal(TimeSpan.FromMinutes(10), st.Total(late.AddDays(1)));

        st.Switch("Code", late.AddDays(20));
        st.Flush(late.AddDays(20).AddMinutes(1));
        Assert.Equal(TimeSpan.Zero, st.Total(late));
        Assert.True(st.Data.Days.Count <= ScreenTime.KeepDays);
    }

    [Fact]
    public void Formats_short()
    {
        Assert.Equal("< 1 m", ScreenTime.Format(TimeSpan.FromSeconds(30)));
        Assert.Equal("45 m", ScreenTime.Format(TimeSpan.FromMinutes(45)));
        Assert.Equal("2 j 5 m", ScreenTime.Format(TimeSpan.FromMinutes(125)));
    }
}

public class LauncherTests
{
    [Fact]
    public void Adds_unique_apps_up_to_the_limit_and_names_them()
    {
        var l = new Peeko.Core.Productivity.Launcher();
        Assert.True(l.Add(@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Visual Studio Code.lnk"));
        Assert.False(l.Add(@"c:\programdata\microsoft\windows\start menu\programs\visual studio code.lnk"));
        Assert.Equal("Visual Studio Code", l.Apps[0].Name);
        Assert.Equal("Proyek", Peeko.Core.Productivity.Launcher.DisplayName(@"D:\Kerja\Proyek\"));
        Assert.Equal("catatan.txt", Peeko.Core.Productivity.Launcher.DisplayName(@"D:\catatan.txt"));
        for (int i = 0; i < 20; i++) l.Add($@"C:\app{i}.exe");
        Assert.Equal(Peeko.Core.Productivity.Launcher.Max, l.Apps.Count);
    }

    [Fact]
    public void Moves_within_bounds()
    {
        var l = new Peeko.Core.Productivity.Launcher();
        l.Add(@"C:\a.exe");
        l.Add(@"C:\b.exe");
        l.Move(l.Apps[0], 1);
        Assert.Equal("b", l.Apps[0].Name);
        l.Move(l.Apps[0], -1);
        Assert.Equal("b", l.Apps[0].Name);
    }
}
