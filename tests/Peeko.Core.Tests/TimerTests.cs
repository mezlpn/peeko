using Microsoft.Extensions.Time.Testing;
using Peeko.Core.Productivity;

namespace Peeko.Core.Tests;

public class CountdownTimerTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 5, 4, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Runs_pauses_and_completes()
    {
        var timer = new CountdownTimer(_time, new TimersSnapshot());
        timer.SetDuration(TimeSpan.FromMinutes(3));
        int done = 0;
        timer.Completed += () => done++;

        timer.Start();
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromMinutes(2), timer.Remaining);

        timer.Pause();
        _time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromMinutes(2), timer.Remaining);
        Assert.Equal(FocusStatus.Paused, timer.Status);

        timer.Start();
        _time.Advance(TimeSpan.FromMinutes(2));
        timer.Tick();
        Assert.Equal(1, done);
        Assert.Equal(FocusStatus.Ready, timer.Status);
        Assert.Equal(TimeSpan.FromMinutes(3), timer.Remaining); // siap diulang dengan durasi yang sama
    }

    [Fact]
    public void Plus_one_minute_while_running_and_survives_reload()
    {
        var snapshot = new TimersSnapshot();
        var timer = new CountdownTimer(_time, snapshot);
        timer.SetDuration(TimeSpan.FromMinutes(1));
        timer.Start();
        timer.Add(TimeSpan.FromMinutes(1));
        _time.Advance(TimeSpan.FromSeconds(30));

        var reloaded = new CountdownTimer(_time, snapshot);
        Assert.Equal(TimeSpan.FromSeconds(90), reloaded.Remaining);
        Assert.Equal(FocusStatus.Running, reloaded.Status);
    }

    [Fact]
    public void Duration_is_clamped()
    {
        var timer = new CountdownTimer(_time, new TimersSnapshot());
        timer.SetDuration(TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromSeconds(10), timer.Duration);
        timer.SetDuration(TimeSpan.FromHours(99));
        Assert.Equal(CountdownTimer.Max, timer.Duration);
    }
}

public class StopwatchTimerTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 5, 4, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Counts_with_pauses_and_laps()
    {
        var sw = new StopwatchTimer(_time, new TimersSnapshot());
        sw.Start();
        _time.Advance(TimeSpan.FromSeconds(30));
        sw.Lap();
        _time.Advance(TimeSpan.FromSeconds(45));
        sw.Lap();
        sw.Pause();
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromSeconds(75), sw.Elapsed);
        Assert.Equal([TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(45)], sw.Laps);

        sw.Reset();
        Assert.Equal(FocusStatus.Ready, sw.Status);
        Assert.Empty(sw.Laps);
    }
}
