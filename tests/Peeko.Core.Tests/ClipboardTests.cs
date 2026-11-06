using Microsoft.Extensions.Time.Testing;
using Peeko.Core.Productivity;

namespace Peeko.Core.Tests;

public class ClipboardHistoryTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 7, 2, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Newest_first_duplicates_move_up_and_pins_stay_on_top()
    {
        var h = new ClipboardHistory(_time);
        h.Add("satu");
        _time.Advance(TimeSpan.FromSeconds(1));
        h.Add("dua");
        _time.Advance(TimeSpan.FromSeconds(1));
        h.Add("satu");
        Assert.Equal(["satu", "dua"], h.Items.Select(i => i.Text));

        h.TogglePin(h.Items[1].Id); // pin "dua"
        Assert.Equal("dua", h.Items[0].Text);
        h.Clear();
        Assert.Equal(["dua"], h.Items.Select(i => i.Text));
        Assert.Single(h.Data.Pinned);
    }

    [Fact]
    public void Keeps_only_recent_unpinned_and_ignores_blank()
    {
        var h = new ClipboardHistory(_time);
        h.Add("   ");
        for (int i = 0; i < ClipboardHistory.MaxRecent + 5; i++)
        {
            h.Add($"item {i}");
            _time.Advance(TimeSpan.FromSeconds(1));
        }
        Assert.Equal(ClipboardHistory.MaxRecent, h.Items.Count);
        Assert.Equal($"item {ClipboardHistory.MaxRecent + 4}", h.Items[0].Text);
        Assert.Equal("a b c", ClipboardHistory.Preview("a\r\n  b\tc"));
    }

    [Fact]
    public void Restores_pinned_items()
    {
        var data = new ClipboardData { Pinned = [new ClipItem { Text = "alamat rumah" }] };
        var h = new ClipboardHistory(_time, data);
        Assert.True(h.Items.Single().Pinned);
    }
}
