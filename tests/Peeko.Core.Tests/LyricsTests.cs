using System.Net;
using System.Text;
using Peeko.Core.Lyrics;
using Microsoft.Extensions.Time.Testing;

namespace Peeko.Core.Tests;

public class LrcParserTests
{
    private static TimeSpan T(int m, int s, int ms = 0) => new(0, 0, m, s, ms);

    [Fact]
    public void Parses_standard_lines()
    {
        var lrc = "[ar:Sela Nada]\n[00:12.34] Di meja ada kertas biru\r\n[00:15.50]kau lipat jadi perahu kecil";
        var lyrics = LrcParser.Parse(lrc);
        Assert.Equal(2, lyrics.Lines.Count);
        Assert.Equal(new LyricLine(T(0, 12, 340), "Di meja ada kertas biru"), lyrics.Lines[0]);
        Assert.Equal(T(0, 15, 500), lyrics.Lines[1].Time);
    }

    [Fact]
    public void Supports_millisecond_and_no_fraction_formats()
    {
        var lyrics = LrcParser.Parse("[01:02.345]a\n[01:03]b\n[1:04.5]c");
        Assert.Equal([T(1, 2, 345), T(1, 3), T(1, 4, 500)], lyrics.Lines.Select(l => l.Time));
    }

    [Fact]
    public void Expands_multiple_timestamps_and_sorts()
    {
        var lyrics = LrcParser.Parse("[00:10.00][00:30.00]Reff\n[00:20.00]Bait");
        Assert.Equal(["Reff", "Bait", "Reff"], lyrics.Lines.Select(l => l.Text));
    }

    [Fact]
    public void Applies_offset_tag()
    {
        // Offset +500 ms: lirik tampil setengah detik lebih awal.
        var lyrics = LrcParser.Parse("[offset:+500]\n[00:10.00]a");
        Assert.Equal(T(0, 9, 500), lyrics.Lines[0].Time);
    }

    [Fact]
    public void Empty_lines_become_single_breaks()
    {
        var lyrics = LrcParser.Parse("[00:01.00]\n[00:05.00]a\n[00:10.00]\n[00:11.00]\n[00:15.00]b\n[00:20.00]");
        Assert.Equal(["a", LrcParser.BreakSymbol, "b"], lyrics.Lines.Select(l => l.Text));
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(5000, 0)]
    [InlineData(9999, 0)]
    [InlineData(10000, 1)]
    [InlineData(99000, 2)]
    public void Index_at_position(int ms, int expected)
    {
        var lyrics = LrcParser.Parse("[00:05.00]a\n[00:10.00]b\n[00:20.00]c");
        Assert.Equal(expected, lyrics.IndexAt(TimeSpan.FromMilliseconds(ms)));
    }
}

public class TrackQueryBuilderTests
{
    private static readonly TimeSpan D = TimeSpan.FromSeconds(200);

    [Fact]
    public void Keeps_clean_music_metadata_as_first_query()
    {
        var q = TrackQueryBuilder.Build("Kita usahakan rumah itu (Alternate Version)", "Sal Priadi", "", D);
        Assert.Equal(("Kita usahakan rumah itu (Alternate Version)", "Sal Priadi"), (q[0].Title, q[0].Artist));
    }

    [Fact]
    public void Splits_youtube_artist_dash_title_and_strips_noise()
    {
        var q = TrackQueryBuilder.Build("Hindia - Secukupnya (Official Music Video)", "Hindia Official", "", D);
        Assert.Contains(q, x => x.Title == "Secukupnya" && x.Artist == "Hindia");
    }

    [Fact]
    public void Strips_topic_channel_and_pipe_tail()
    {
        var q = TrackQueryBuilder.Build("Bertaut | Live at Studio", "Nadin Amizah - Topic", "", D);
        Assert.Contains(q, x => x.Title == "Bertaut" && x.Artist == "Nadin Amizah");
    }

    [Fact]
    public void Adds_base_title_without_version_note_as_second_query()
    {
        var q = TrackQueryBuilder.Build("Kita usahakan rumah itu (Alternate Version)", "Sal Priadi", "", D);
        Assert.Equal(("Kita usahakan rumah itu", "Sal Priadi"), (q[1].Title, q[1].Artist));
    }

    [Fact]
    public void No_duplicate_queries()
    {
        var q = TrackQueryBuilder.Build("Secukupnya", "Hindia", "", D);
        Assert.Single(q);
    }

    [Fact]
    public void Cache_key_ignores_case_and_rounds_duration()
    {
        var a = new TrackQuery("Lagu", "Artis", "", TimeSpan.FromSeconds(199.6));
        var b = new TrackQuery("lagu ", "ARTIS", "x", TimeSpan.FromSeconds(200.2));
        Assert.Equal(a.CacheKey, b.CacheKey);
    }
}

public sealed class LyricsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "peeko-test-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
    private readonly FakeHandler _handler = new();
    private readonly LyricsService _service;

    private static readonly IReadOnlyList<TrackQuery> Query =
        [new TrackQuery("Kertas Biru", "Sela Nada", "", TimeSpan.FromSeconds(198))];

    public LyricsServiceTests()
    {
        _service = new LyricsService(new HttpClient(_handler), _dir, _time);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Exact_match_returns_synced_lyrics_and_is_cached()
    {
        _handler.Respond("/api/get", """{"duration":198,"instrumental":false,"plainLyrics":"a","syncedLyrics":"[00:01.00]a"}""");

        var first = await _service.GetAsync(Query, false, default);
        var second = await _service.GetAsync(Query, false, default);

        Assert.IsType<LyricsResult.Synced>(first);
        Assert.IsType<LyricsResult.Synced>(second);
        Assert.Equal(1, _handler.Calls);
    }

    [Fact]
    public async Task Falls_back_to_search_and_picks_matching_duration_with_sync()
    {
        _handler.Respond("/api/get", null, HttpStatusCode.NotFound);
        _handler.Respond("/api/search", """
            [
              {"duration":260,"instrumental":false,"plainLyrics":"x","syncedLyrics":"[00:01.00]salah durasi"},
              {"duration":199,"instrumental":false,"plainLyrics":"cocok","syncedLyrics":null},
              {"duration":197,"instrumental":false,"plainLyrics":"cocok","syncedLyrics":"[00:01.00]cocok"}
            ]
            """);

        var result = Assert.IsType<LyricsResult.Synced>(await _service.GetAsync(Query, false, default));
        Assert.Equal("cocok", result.Lyrics.Lines[0].Text);
    }

    [Fact]
    public async Task Plain_exact_match_does_not_stop_search_for_synced_version()
    {
        // Kasus nyata: versi "(Alternate Version)" hanya punya teks biasa di LRCLIB.
        _handler.Respond("/api/get", """{"duration":198,"instrumental":false,"plainLyrics":"teks","syncedLyrics":null}""");
        _handler.Respond("/api/search", """[{"duration":197,"instrumental":false,"plainLyrics":"teks","syncedLyrics":"[00:01.00]teks"}]""");

        Assert.IsType<LyricsResult.Synced>(await _service.GetAsync(Query, false, default));
    }

    [Fact]
    public async Task Plain_result_is_used_when_no_synced_exists()
    {
        _handler.Respond("/api/get", """{"duration":198,"instrumental":false,"plainLyrics":"teks","syncedLyrics":null}""");
        _handler.Respond("/api/search", "[]");

        Assert.IsType<LyricsResult.Plain>(await _service.GetAsync(Query, false, default));
    }

    [Fact]
    public async Task Instrumental_is_reported()
    {
        _handler.Respond("/api/get", """{"duration":198,"instrumental":true,"plainLyrics":null,"syncedLyrics":null}""");
        Assert.IsType<LyricsResult.Instrumental>(await _service.GetAsync(Query, false, default));
    }

    [Fact]
    public async Task Not_found_is_cached_for_seven_days_unless_retry()
    {
        _handler.Respond("/api/get", null, HttpStatusCode.NotFound);
        _handler.Respond("/api/search", "[]");

        Assert.IsType<LyricsResult.NotFound>(await _service.GetAsync(Query, false, default));
        int calls = _handler.Calls;

        await _service.GetAsync(Query, false, default);
        Assert.Equal(calls, _handler.Calls); // dari cache

        await _service.GetAsync(Query, retryNotFound: true, default);
        Assert.True(_handler.Calls > calls); // "Cari lagi" menghubungi server

        calls = _handler.Calls;
        _time.Advance(TimeSpan.FromDays(8));
        await _service.GetAsync(Query, false, default);
        Assert.True(_handler.Calls > calls); // kedaluwarsa
    }

    [Fact]
    public async Task Network_failure_is_not_cached()
    {
        _handler.Fail = true;
        Assert.IsType<LyricsResult.Failed>(await _service.GetAsync(Query, false, default));

        _handler.Fail = false;
        _handler.Respond("/api/get", """{"duration":198,"instrumental":false,"plainLyrics":"teks","syncedLyrics":null}""");
        Assert.IsType<LyricsResult.Plain>(await _service.GetAsync(Query, false, default));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (string? Body, HttpStatusCode Status)> _routes = [];

        public int Calls { get; private set; }
        public bool Fail { get; set; }

        public void Respond(string path, string? body, HttpStatusCode status = HttpStatusCode.OK) =>
            _routes[path] = (body, status);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (Fail) throw new HttpRequestException("offline");

            var path = request.RequestUri!.AbsolutePath;
            var (body, status) = _routes.TryGetValue(path, out var r) ? r : (null, HttpStatusCode.NotFound);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body ?? """{"code":404}""", Encoding.UTF8, "application/json"),
            });
        }
    }
}
