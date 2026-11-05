using Peeko.Core.Prayer;

namespace Peeko.Core.Tests;

public class PrayerTimesTests
{
    private static readonly TimeSpan Wib = TimeSpan.FromHours(7);

    /// <summary>Jadwal resmi Kemenag (api.myquran.com), diambil 5 Nov 2026.</summary>
    [Theory]
    [InlineData(-7.8014, 110.3647, 2026, 11, 5, "03:41 03:51 05:07 11:26 14:41 17:38 18:50")] // Kota Yogyakarta
    [InlineData(-7.8014, 110.3647, 2026, 1, 15, "04:00 04:10 05:29 11:51 15:15 18:07 19:21")]
    [InlineData(-6.1744, 106.8294, 2026, 6, 21, "04:30 04:40 05:58 11:58 15:19 17:51 19:05")] // Kota Jakarta
    public void Matches_Kemenag_within_one_minute(double lat, double lon, int y, int m, int d, string kemenag)
    {
        var times = PrayerTimes.For(new DateOnly(y, m, d), lat, lon, Wib);
        var expected = kemenag.Split(' ').Select(t => new DateOnly(y, m, d).ToDateTime(TimeOnly.Parse(t))).ToList();

        Assert.Equal(7, times.Count);
        for (int i = 0; i < 7; i++)
            Assert.InRange((times[i].At - expected[i]).TotalMinutes, -1, 1);
    }

    [Fact]
    public void Order_is_imsak_to_isya_and_increasing()
    {
        var times = PrayerTimes.For(new DateOnly(2026, 3, 20), -7.8, 110.36, Wib);
        Assert.Equal(Enum.GetValues<PrayerName>(), times.Select(t => t.Name));
        Assert.True(times.Zip(times.Skip(1)).All(p => p.First.At < p.Second.At));
        Assert.Equal(5, times.Count(t => t.IsPrayer));
    }

    [Fact]
    public void Next_skips_markers_and_rolls_over_to_tomorrows_subuh()
    {
        IReadOnlyList<PrayerTime> At(DateOnly day) => PrayerTimes.For(day, -7.8014, 110.3647, Wib);
        var today = new DateOnly(2026, 11, 5);

        // 04:30: Terbit (05:07) bukan salat, jadi berikutnya Dzuhur.
        Assert.Equal(PrayerName.Dzuhur, PrayerTimes.Next(today.ToDateTime(new TimeOnly(4, 30)), At).Name);
        // Setelah Isya: Subuh besok.
        var next = PrayerTimes.Next(today.ToDateTime(new TimeOnly(21, 0)), At);
        Assert.Equal(PrayerName.Subuh, next.Name);
        Assert.Equal(today.AddDays(1), DateOnly.FromDateTime(next.At));
    }

    [Fact]
    public void Works_far_from_the_equator()
    {
        // London, musim panas: Isya −18° hampir tidak tercapai; tetap menghasilkan jadwal berurutan.
        var times = PrayerTimes.For(new DateOnly(2026, 6, 21), 51.5074, -0.1278, TimeSpan.FromHours(1));
        Assert.All(times, t => Assert.Equal(new DateOnly(2026, 6, 21), DateOnly.FromDateTime(t.At)));
    }
}

public class PrayerActivityTests
{
    [Fact]
    public void Prayer_countdown_beats_music_timer_and_update_but_not_water()
    {
        Assert.Equal(Peeko.Core.Notch.CollapsedActivity.Prayer, Peeko.Core.Notch.ActivityArbiter.Pick(
            musicLive: true, Peeko.Core.Productivity.FocusStatus.Running, waterReminder: false, updateAvailable: true, prayerSoon: true));
        Assert.Equal(Peeko.Core.Notch.CollapsedActivity.Water, Peeko.Core.Notch.ActivityArbiter.Pick(
            musicLive: false, Peeko.Core.Productivity.FocusStatus.Ready, waterReminder: true, prayerSoon: true));
    }
}
