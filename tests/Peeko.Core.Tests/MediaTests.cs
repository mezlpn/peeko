using Peeko.Core.Media;

namespace Peeko.Core.Tests;

public class PlaybackClockTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Song = TimeSpan.FromMinutes(3);

    private static MediaTimeline At(double positionSec, DateTimeOffset reportedAt) =>
        new(TimeSpan.FromSeconds(positionSec), Song, reportedAt);

    [Fact]
    public void Extrapolates_while_playing()
    {
        var clock = new PlaybackClock();
        clock.Update(At(10, T0), playing: true, rate: 1, now: T0);
        Assert.Equal(TimeSpan.FromSeconds(15), clock.GetPosition(T0.AddSeconds(5)));
    }

    [Fact]
    public void Uses_report_time_not_receive_time()
    {
        var clock = new PlaybackClock();
        // Laporan dibuat 2 dtk sebelum kita menerimanya.
        clock.Update(At(10, T0), playing: true, rate: 1, now: T0.AddSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(12), clock.GetPosition(T0.AddSeconds(2)));
    }

    [Fact]
    public void Pause_without_new_report_freezes_at_computed_position()
    {
        var clock = new PlaybackClock();
        clock.Update(At(10, T0), playing: true, rate: 1, now: T0);
        // Pemutar hanya mengubah status, timeline lama dikirim ulang.
        clock.Update(At(10, T0), playing: false, rate: 1, now: T0.AddSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(40), clock.GetPosition(T0.AddSeconds(90)));
    }

    [Fact]
    public void Resume_without_new_report_continues_from_frozen_position()
    {
        var clock = new PlaybackClock();
        clock.Update(At(10, T0), playing: false, rate: 1, now: T0);
        clock.Update(At(10, T0), playing: true, rate: 1, now: T0.AddSeconds(60));
        Assert.Equal(TimeSpan.FromSeconds(15), clock.GetPosition(T0.AddSeconds(65)));
    }

    [Fact]
    public void New_report_reanchors_after_seek()
    {
        var clock = new PlaybackClock();
        clock.Update(At(10, T0), playing: true, rate: 1, now: T0);
        clock.Update(At(100, T0.AddSeconds(5)), playing: true, rate: 1, now: T0.AddSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(101), clock.GetPosition(T0.AddSeconds(6)));
    }

    [Fact]
    public void Broken_report_time_falls_back_to_now()
    {
        var clock = new PlaybackClock();
        clock.Update(At(10, new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero)), playing: true, rate: 1, now: T0);
        Assert.Equal(TimeSpan.FromSeconds(13), clock.GetPosition(T0.AddSeconds(3)));
    }

    [Fact]
    public void Clamps_to_duration()
    {
        var clock = new PlaybackClock();
        clock.Update(At(170, T0), playing: true, rate: 1, now: T0);
        Assert.Equal(Song, clock.GetPosition(T0.AddMinutes(5)));
    }

    [Fact]
    public void Honors_playback_rate()
    {
        var clock = new PlaybackClock();
        clock.Update(At(0, T0), playing: true, rate: 1.5, now: T0);
        Assert.Equal(TimeSpan.FromSeconds(15), clock.GetPosition(T0.AddSeconds(10)));
    }
}

public class MediaSourceAppTests
{
    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("Chrome", "Chrome")]
    [InlineData("MSEdge", "Edge")]
    [InlineData("308046B0AF4A39CB", "Firefox")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Media Player")]
    [InlineData("Contoso.Pemutar_abc123!App", "Pemutar")]
    [InlineData("aimp.exe", "Aimp")]
    [InlineData("", "Pemutar")]
    public void Maps_app_id_to_friendly_name(string appId, string expected)
    {
        Assert.Equal(expected, MediaSourceApp.FromAppId(appId).Name);
    }
}

public class CoverPaletteTests
{
    private static byte[] Solid(byte r, byte g, byte b, int count = 1000)
    {
        var px = new byte[count * 4];
        for (int i = 0; i < count; i++)
        {
            px[i * 4] = b; px[i * 4 + 1] = g; px[i * 4 + 2] = r; px[i * 4 + 3] = 255;
        }
        return px;
    }

    [Fact]
    public void Light_saturated_color_is_kept()
    {
        var c = CoverPalette.DominantColor(Solid(0xE8, 0x83, 0x6B));
        Assert.InRange(c.R, 0xE0, 0xF0);
        Assert.InRange(c.G, 0x7B, 0x8B);
    }

    [Fact]
    public void Dark_color_is_lifted_to_min_lightness()
    {
        var c = CoverPalette.DominantColor(Solid(0x3A, 0x1A, 0x12));
        var (_, _, l) = CoverPalette.ToHsl(c.R, c.G, c.B);
        Assert.InRange(l, 0.63, 0.67);
    }

    [Fact]
    public void Grayscale_cover_uses_fallback_accent()
    {
        Assert.Equal(CoverPalette.Fallback, CoverPalette.DominantColor(Solid(0x80, 0x80, 0x80)));
    }

    [Fact]
    public void Saturated_area_beats_larger_gray_background()
    {
        var gray = Solid(0x50, 0x50, 0x55, 3000);
        var red = Solid(0xE0, 0x30, 0x30, 1000);
        var c = CoverPalette.DominantColor([.. gray, .. red]);
        Assert.True(c.R > c.G + 50, $"Diharapkan merah, dapat {c.ToHex()}");
    }
}

public class SpectrumAnalyzerTests
{
    private static float[] Sine(double bin, int samples, double amplitude = 0.5) =>
        Enumerable.Range(0, samples)
            .Select(i => (float)(amplitude * Math.Sin(2 * Math.PI * bin * i / SpectrumAnalyzer.FftSize)))
            .ToArray();

    [Fact]
    public void Silence_stays_at_floor()
    {
        var analyzer = new SpectrumAnalyzer();
        Assert.True(analyzer.Push(new float[SpectrumAnalyzer.FftSize]));
        Assert.All(analyzer.Levels, l => Assert.Equal(SpectrumAnalyzer.Floor, l));
    }

    [Fact]
    public void Needs_a_full_window_before_updating()
    {
        var analyzer = new SpectrumAnalyzer();
        Assert.False(analyzer.Push(new float[SpectrumAnalyzer.FftSize - 1]));
        Assert.True(analyzer.Push(new float[1]));
    }

    [Theory]
    [InlineData(4, 1)]    // ≈ 375 Hz → pita 2
    [InlineData(10, 2)]   // ≈ 940 Hz → pita 3
    [InlineData(100, 4)]  // ≈ 9.4 kHz → pita 5
    public void Tone_lights_up_its_own_band(double bin, int band)
    {
        var analyzer = new SpectrumAnalyzer();
        for (int i = 0; i < 8; i++) analyzer.Push(Sine(bin, SpectrumAnalyzer.FftSize));
        var levels = analyzer.Levels;
        Assert.Equal(band, Array.IndexOf(levels, levels.Max()));
        Assert.True(levels[band] > 0.8f);
    }

    [Fact]
    public void Levels_fall_back_after_the_music_stops()
    {
        var analyzer = new SpectrumAnalyzer();
        for (int i = 0; i < 8; i++) analyzer.Push(Sine(10, SpectrumAnalyzer.FftSize));
        float loud = analyzer.Levels[2];
        for (int i = 0; i < 8; i++) analyzer.Push(new float[SpectrumAnalyzer.FftSize]);
        Assert.True(analyzer.Levels[2] < loud / 2);
    }
}
