using Celah.Core.Notch;
using Microsoft.Extensions.Time.Testing;

namespace Celah.Core.Tests;

public class NotchStateMachineTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly NotchStateMachine _sm;

    public NotchStateMachineTests()
    {
        _sm = new NotchStateMachine(_time, a => a());
    }

    private void Wait(int ms) => _time.Advance(TimeSpan.FromMilliseconds(ms));

    [Fact]
    public void Starts_collapsed()
    {
        Assert.Equal(NotchState.Collapsed, _sm.State);
    }

    [Fact]
    public void Hover_expands_after_delay()
    {
        _sm.PointerEntered();
        Wait(119);
        Assert.Equal(NotchState.Collapsed, _sm.State);
        Wait(1);
        Assert.Equal(NotchState.Expanded, _sm.State);
    }

    [Fact]
    public void Brief_hover_does_not_expand()
    {
        _sm.PointerEntered();
        Wait(80);
        _sm.PointerExited();
        Wait(500);
        Assert.Equal(NotchState.Collapsed, _sm.State);
    }

    [Fact]
    public void Clickable_area_uses_longer_delay()
    {
        _sm.PointerEntered(TimeSpan.FromMilliseconds(400));
        Wait(300);
        Assert.Equal(NotchState.Collapsed, _sm.State);
        Wait(100);
        Assert.Equal(NotchState.Expanded, _sm.State);
    }

    [Fact]
    public void Leaving_collapses_after_delay_and_returning_cancels()
    {
        _sm.PointerEntered();
        Wait(120);
        _sm.PointerExited();
        Wait(300);
        _sm.PointerEntered();
        Wait(1000);
        Assert.Equal(NotchState.Expanded, _sm.State);

        _sm.PointerExited();
        Wait(350);
        Assert.Equal(NotchState.Collapsed, _sm.State);
    }

    [Fact]
    public void Hold_open_keeps_expanded_until_released()
    {
        _sm.PointerEntered();
        Wait(120);
        _sm.SetHoldOpen(true);
        _sm.PointerExited();
        Wait(2000);
        Assert.Equal(NotchState.Expanded, _sm.State);

        _sm.SetHoldOpen(false);
        Wait(350);
        Assert.Equal(NotchState.Collapsed, _sm.State);
    }

    [Fact]
    public void Fullscreen_hides_and_returns_after_settle()
    {
        _sm.SetFullscreen(true);
        Assert.Equal(NotchState.Hidden, _sm.State);

        _sm.SetFullscreen(false);
        Wait(399);
        Assert.Equal(NotchState.Hidden, _sm.State);
        Wait(1);
        Assert.Equal(NotchState.Collapsed, _sm.State);
    }

    [Fact]
    public void Quick_alt_tab_back_into_fullscreen_stays_hidden()
    {
        _sm.SetFullscreen(true);
        _sm.SetFullscreen(false);
        Wait(200);
        _sm.SetFullscreen(true);
        Wait(1000);
        Assert.Equal(NotchState.Hidden, _sm.State);
    }

    [Fact]
    public void Fullscreen_while_expanded_hides_and_cancels_pending_close()
    {
        _sm.PointerEntered();
        Wait(120);
        _sm.PointerExited();
        _sm.SetFullscreen(true);
        Wait(1000);
        Assert.Equal(NotchState.Hidden, _sm.State);
    }

    [Fact]
    public void Peek_returns_to_collapsed_after_hold()
    {
        Assert.True(_sm.ShowPeek());
        Assert.Equal(NotchState.Peek, _sm.State);
        Wait(1500);
        Assert.Equal(NotchState.Collapsed, _sm.State);
    }

    [Fact]
    public void Repeated_peek_resets_hold()
    {
        _sm.ShowPeek();
        Wait(1000);
        _sm.ShowPeek();
        Wait(1000);
        Assert.Equal(NotchState.Peek, _sm.State);
        Wait(500);
        Assert.Equal(NotchState.Collapsed, _sm.State);
    }

    [Fact]
    public void Peek_is_rejected_while_expanded_or_hidden()
    {
        _sm.PointerEntered();
        Wait(120);
        Assert.False(_sm.ShowPeek());

        _sm.SetFullscreen(true);
        Assert.False(_sm.ShowPeek());
        Assert.Equal(NotchState.Hidden, _sm.State);
    }

    [Fact]
    public void Critical_peek_bypasses_fullscreen_then_hides_again()
    {
        _sm.SetFullscreen(true);
        Assert.True(_sm.ShowPeek(TimeSpan.FromSeconds(5), bypassHidden: true));
        Assert.Equal(NotchState.Peek, _sm.State);
        Wait(5000);
        Assert.Equal(NotchState.Hidden, _sm.State);
    }

    [Fact]
    public void Hovering_peek_holds_it_then_exit_restarts_hold()
    {
        _sm.ShowPeek();
        _sm.PointerEntered(TimeSpan.FromSeconds(10)); // jangan sampai mengembang di test ini
        Wait(3000);
        Assert.Equal(NotchState.Peek, _sm.State);

        _sm.PointerExited();
        Wait(1499);
        Assert.Equal(NotchState.Peek, _sm.State);
        Wait(1);
        Assert.Equal(NotchState.Collapsed, _sm.State);
    }

    [Fact]
    public void Suspend_hides_and_resume_restores()
    {
        _sm.SetSuspended(true);
        Assert.Equal(NotchState.Hidden, _sm.State);
        _sm.SetSuspended(false);
        Assert.Equal(NotchState.Collapsed, _sm.State);
    }

    [Fact]
    public void Resume_while_fullscreen_stays_hidden()
    {
        _sm.SetSuspended(true);
        _sm.SetFullscreen(true);
        _sm.SetSuspended(false);
        Assert.Equal(NotchState.Hidden, _sm.State);
    }

    [Fact]
    public void State_changed_reports_previous_and_next()
    {
        var log = new List<(NotchState, NotchState)>();
        _sm.StateChanged += (a, b) => log.Add((a, b));
        _sm.PointerEntered();
        Wait(120);
        Assert.Equal([(NotchState.Collapsed, NotchState.Expanded)], log);
    }
}
