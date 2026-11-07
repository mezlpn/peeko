using Peeko.Core.Settings;
using Xunit;

namespace Peeko.Core.Tests;

public class ThemeTests
{
    [Fact]
    public void DefaultAccentMatchesOriginalTokens()
    {
        var p = ThemePalette.Build(null, NotchStyle.Black);
        Assert.Equal((0xC6, 0xF3, 0x6B), (p.Accent.R, p.Accent.G, p.Accent.B));
        // Token asli: muted #202614, pressed #A9D84F, hover #D3F785 (selisih pembulatan ≤ 2).
        Assert.InRange(p.Muted.R, 0x1E, 0x22);
        Assert.InRange(p.Muted.G, 0x24, 0x28);
        Assert.InRange(p.Pressed.R, 0xA7, 0xAB);
        Assert.InRange(p.Hover.G, 0xF4, 0xF8);
        Assert.Equal(255, p.NotchBg.A);
    }

    [Fact]
    public void DarkAccentGetsWhiteInk()
    {
        var p = ThemePalette.Build("#3050C0", NotchStyle.Black);
        Assert.Equal((255, 255, 255), (p.Ink.R, p.Ink.G, p.Ink.B));
        Assert.True(ThemePalette.Build("#E8E8EC", NotchStyle.Black).Ink.R < 0x20);
    }

    [Fact]
    public void GlassIsTranslucentAndTintedLeansToAccent()
    {
        Assert.True(ThemePalette.Build(null, NotchStyle.Glass).NotchBg.A < 255);
        var tinted = ThemePalette.Build("#FF0000", NotchStyle.Tinted).NotchBg;
        Assert.True(tinted.R > tinted.G);
    }

    [Theory]
    [InlineData("zzz")]
    [InlineData("#12")]
    [InlineData("")]
    public void BadHexFallsBack(string hex) =>
        Assert.Equal(ThemePalette.DefaultAccent, ThemePalette.ParseOrDefault(hex));
}
