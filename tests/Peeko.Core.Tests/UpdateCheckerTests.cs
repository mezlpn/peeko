using System.Net;
using System.Text;
using Peeko.Core.Updates;

namespace Peeko.Core.Tests;

public class UpdateCheckerTests
{
    private sealed class Feed(string? body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }
        public string? ETag { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            if (ETag is not null && request.Headers.IfNoneMatch.Any(t => t.Tag == ETag))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body ?? "{}", Encoding.UTF8, "application/json"),
            };
            if (ETag is not null) response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(ETag);
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task Unchanged_release_uses_etag_and_keeps_result()
    {
        var feed = new Feed(Release) { ETag = "\"abc\"" };
        var checker = new UpdateChecker(new HttpClient(feed));

        var first = await checker.GetLatestAsync(default);
        Assert.Empty(feed.Last!.Headers.IfNoneMatch);

        var second = await checker.GetLatestAsync(default);
        Assert.Contains(feed.Last!.Headers.IfNoneMatch, t => t.Tag == "\"abc\"");
        Assert.Equal(first, second);
    }

    private const string Release = """
        {
          "tag_name": "v0.1.2",
          "html_url": "https://github.com/mezlpn/peeko/releases/tag/v0.1.2",
          "draft": false,
          "prerelease": false,
          "assets": [
            { "name": "catatan.txt", "size": 10, "browser_download_url": "https://x/catatan.txt" },
            { "name": "Peeko-0.1.2-win-x64.zip", "size": 73400320, "browser_download_url": "https://x/Peeko-0.1.2-win-x64.zip" }
          ]
        }
        """;

    [Fact]
    public async Task Reads_version_and_windows_zip_asset()
    {
        var feed = new Feed(Release);
        var info = await new UpdateChecker(new HttpClient(feed)).GetLatestAsync(default);

        Assert.NotNull(info);
        Assert.Equal(new Version(0, 1, 2), info.Version);
        Assert.Equal("Peeko-0.1.2-win-x64.zip", info.AssetName);
        Assert.Equal("https://x/Peeko-0.1.2-win-x64.zip", info.AssetUrl);
        Assert.Contains("application/vnd.github+json", feed.Last!.Headers.Accept.ToString());
    }

    [Fact]
    public async Task Release_without_windows_zip_is_ignored()
    {
        var body = Release.Replace("Peeko-0.1.2-win-x64.zip", "Peeko-0.1.2-mac.dmg");
        Assert.Null(await new UpdateChecker(new HttpClient(new Feed(body))).GetLatestAsync(default));
    }

    [Fact]
    public async Task No_release_yet_returns_null()
    {
        Assert.Null(await new UpdateChecker(new HttpClient(new Feed(null, HttpStatusCode.NotFound))).GetLatestAsync(default));
    }

    [Fact]
    public async Task Server_error_throws_so_caller_can_retry_later()
    {
        var checker = new UpdateChecker(new HttpClient(new Feed(null, HttpStatusCode.ServiceUnavailable)));
        await Assert.ThrowsAsync<HttpRequestException>(() => checker.GetLatestAsync(default));
    }

    [Theory]
    [InlineData("v0.1.2", "0.1.2")]
    [InlineData("0.2.0", "0.2.0")]
    [InlineData("v1.0", "1.0.0")]
    [InlineData("v1", "1.0.0")]
    [InlineData("v0.3.0-beta", "0.3.0")]
    public void Parses_tags(string tag, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateChecker.TryParseVersion(tag));

    [Theory]
    [InlineData("terbaru")]
    [InlineData("")]
    public void Rejects_non_version_tags(string tag) => Assert.Null(UpdateChecker.TryParseVersion(tag));

    [Theory]
    [InlineData("0.1.1.0", "0.1.2", true)]
    [InlineData("0.1.2.0", "0.1.2", false)]
    [InlineData("0.2.0.0", "0.1.9", false)]
    [InlineData("0.1.9", "1.0.0", true)]
    public void Compares_versions(string current, string latest, bool newer) =>
        Assert.Equal(newer, UpdateChecker.IsNewer(Version.Parse(current), Version.Parse(latest)));
}
