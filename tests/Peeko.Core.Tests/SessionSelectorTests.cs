using Peeko.Core.Media;

namespace Peeko.Core.Tests;

public class SessionSelectorTests
{
    private sealed class S(string name)
    {
        public override string ToString() => name;
    }

    private readonly S _chrome = new("chrome");
    private readonly S _brave = new("brave");
    private readonly SessionSelector<S> _sel = new();

    [Fact]
    public void Picks_playing_session_first()
    {
        Assert.Same(_brave, _sel.Update([(_chrome, false), (_brave, true)], systemCurrent: _chrome));
    }

    [Fact]
    public void Paused_selection_stays_even_if_windows_current_moves()
    {
        // Kasus pengguna: Brave (YouTube Music) diputar, lalu dijeda.
        _sel.Update([(_chrome, false), (_brave, true)], _brave);
        Assert.Same(_brave, _sel.Update([(_chrome, false), (_brave, false)], systemCurrent: _chrome));
    }

    [Fact]
    public void Paused_selection_stays_when_other_is_already_playing()
    {
        _sel.Update([(_chrome, true), (_brave, true)], _brave);
        _sel.Cycle([_chrome, _brave], 1); // pilih brave
        Assert.Same(_brave, _sel.Update([(_chrome, true), (_brave, false)], _chrome));
    }

    [Fact]
    public void Session_that_starts_playing_takes_over()
    {
        _sel.Update([(_chrome, false), (_brave, true)], _brave);
        _sel.Update([(_chrome, false), (_brave, false)], _brave);
        Assert.Same(_chrome, _sel.Update([(_chrome, true), (_brave, false)], _chrome));
    }

    [Fact]
    public void Manual_choice_that_is_playing_is_not_taken_over()
    {
        _sel.Update([(_chrome, false), (_brave, true)], _brave);
        _sel.Cycle([_chrome, _brave], -1); // pindah ke chrome manual
        _sel.Update([(_chrome, true), (_brave, false)], _chrome);

        // Brave mulai diputar lagi, tapi pilihan manual (Chrome) masih diputar.
        Assert.Same(_chrome, _sel.Update([(_chrome, true), (_brave, true)], _brave));
    }

    [Fact]
    public void Manual_choice_that_is_paused_can_be_taken_over()
    {
        _sel.Update([(_chrome, false), (_brave, true)], _brave);
        _sel.Cycle([_chrome, _brave], 1); // ke chrome (dijeda)
        Assert.Same(_chrome, _sel.Update([(_chrome, false), (_brave, false)], _brave));
        // Brave mulai diputar lagi; pilihan manual sedang dijeda, jadi Brave mengambil alih.
        Assert.Same(_brave, _sel.Update([(_chrome, false), (_brave, true)], _brave));
    }

    [Fact]
    public void Closed_session_falls_back()
    {
        _sel.Update([(_chrome, false), (_brave, true)], _brave);
        Assert.Same(_chrome, _sel.Update([(_chrome, false)], systemCurrent: null));
    }

    [Fact]
    public void Cycle_wraps_around()
    {
        _sel.Update([(_chrome, true), (_brave, false)], _chrome);
        Assert.Same(_brave, _sel.Cycle([_chrome, _brave], 1));
        Assert.Same(_chrome, _sel.Cycle([_chrome, _brave], 1));
        Assert.Same(_brave, _sel.Cycle([_chrome, _brave], -1));
    }

    [Fact]
    public void No_sessions_selects_nothing()
    {
        Assert.Null(_sel.Update([], null));
    }
}
