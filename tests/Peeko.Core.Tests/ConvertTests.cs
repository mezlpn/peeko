using System.Text.Json;
using Peeko.Core.Conversion;

namespace Peeko.Core.Tests;

public class FileeOutputTests
{
    // Keluaran asli filee-cli 1.12.0.
    private const string PngFormats = """
        PNG (png) converts to:
          jpg, webp, tiff, bmp, gif, ico, avif, pdf, docx, txt
        With optional engines (install them in Filee, Settings → Engines):
          heic, eps, mp4, mp3, pdf
        """;

    [Fact]
    public void Parses_ready_and_optional_targets()
    {
        var targets = FileeOutput.ParseFormats(PngFormats)!;
        Assert.Equal("PNG", targets.Name);
        Assert.Equal(["jpg", "webp", "tiff", "bmp", "gif", "ico", "avif", "pdf", "docx", "txt"], targets.Ready);
        // "pdf" sudah bisa tanpa mesin: tidak diulang di daftar opsional.
        Assert.Equal(["heic", "eps", "mp4", "mp3"], targets.NeedsEngine);
    }

    [Fact]
    public void Handles_crlf_and_missing_optional_section()
    {
        var targets = FileeOutput.ParseFormats("TTF (ttf) converts to:\r\n  otf, woff, woff2, eot\r\n")!;
        Assert.Equal(["otf", "woff", "woff2", "eot"], targets.Ready);
        Assert.Empty(targets.NeedsEngine);
    }

    [Fact]
    public void Nothing_ready_without_engines()
    {
        const string mp4 = """
            MP4 (mp4) converts to:
              (nothing with the engines installed now)
            With optional engines (install them in Filee, Settings → Engines):
              png, gif, mp3
            """;
        var targets = FileeOutput.ParseFormats(mp4)!;
        Assert.Empty(targets.Ready);
        Assert.Equal(["png", "gif", "mp3"], targets.NeedsEngine);
    }

    [Fact]
    public void Unknown_format_gives_null()
    {
        Assert.Null(FileeOutput.ParseFormats("filee: unknown format 'xyz'."));
        Assert.Null(FileeOutput.ParseFormats(""));
    }

    [Fact]
    public void Parses_successful_conversion()
    {
        const string json = """
            {
              "preset": "JPG", "target": "jpg", "converted": 1, "skipped": 0, "failed": 0,
              "files": [ { "source": "C:\\a\\uji coba.png", "state": "done", "outputs": [ "C:\\a\\uji coba.jpg" ] } ]
            }
            """;
        var result = FileeOutput.ParseResult(json);
        Assert.Equal("jpg", result.Target);
        Assert.Equal(1, result.Converted);
        Assert.Equal([@"C:\a\uji coba.jpg"], result.Outputs);
        Assert.Null(result.FirstError);
    }

    [Fact]
    public void Parses_failure_with_message()
    {
        const string json = """
            {
              "preset": "MP4", "target": "mp4", "converted": 0, "skipped": 0, "failed": 1,
              "files": [ { "source": "C:\\a\\b.png", "state": "failed", "outputs": [], "error": "No engine can do this conversion (png \u2192 mp4)" } ]
            }
            """;
        var result = FileeOutput.ParseResult(json);
        Assert.Equal(1, result.Failed);
        Assert.Empty(result.Outputs);
        Assert.Equal("No engine can do this conversion (png → mp4)", result.FirstError);
    }

    [Fact]
    public void Multi_page_output_lists_every_file()
    {
        const string json = """
            { "target": "png", "converted": 1, "files": [ { "source": "a.pdf", "state": "done", "outputs": [ "a-1.png", "a-2.png" ] } ] }
            """;
        Assert.Equal(["a-1.png", "a-2.png"], FileeOutput.ParseResult(json).Outputs);
    }

    [Fact]
    public void Garbage_throws_json_exception()
    {
        Assert.ThrowsAny<JsonException>(() => FileeOutput.ParseResult("filee: not found: x"));
    }
}

public class ConvertSuggestionsTests
{
    private static readonly string[] ImageTargets = ["jpg", "webp", "tiff", "bmp", "gif", "ico", "avif", "pdf", "docx", "txt"];

    [Fact]
    public void Image_gets_image_formats_first_without_itself()
    {
        Assert.Equal(["jpg", "webp", "pdf", "ico", "gif"], ConvertSuggestions.Pick(ImageTargets, [".PNG"]));
    }

    [Fact]
    public void Pdf_prefers_word_and_pictures()
    {
        string[] pdfTargets = ["png", "jpg", "tiff", "docx", "hwpx", "txt", "pptx", "cbz"];
        Assert.Equal(["docx", "png", "jpg", "txt", "pptx"], ConvertSuggestions.Pick(pdfTargets, ["pdf"]));
    }

    [Fact]
    public void Fills_up_from_general_list_and_respects_max()
    {
        string[] targets = ["pdf", "zip", "txt", "xlsx"];
        Assert.Equal(["pdf", "xlsx", "txt"], ConvertSuggestions.Pick(targets, ["csv"], max: 3));
    }

    [Fact]
    public void Unlisted_targets_still_fill_the_chips()
    {
        Assert.Equal(["woff", "woff2", "otf"], ConvertSuggestions.Pick(["woff", "woff2", "otf"], ["ttf"]));
    }

    [Fact]
    public void Mixed_sources_use_general_order()
    {
        Assert.Equal(["pdf", "docx", "txt"], ConvertSuggestions.Pick(["txt", "docx", "pdf"], ["png", "docx"]));
    }

    [Fact]
    public void Intersection_keeps_first_list_order()
    {
        IReadOnlyList<string> a = ["pdf", "png", "jpg"];
        IReadOnlyList<string> b = ["JPG", "pdf"];
        Assert.Equal(["pdf", "jpg"], ConvertSuggestions.Intersect([a, b]));
        Assert.Empty(ConvertSuggestions.Intersect([]));
    }

    [Theory]
    [InlineData("mov", "mp4")]
    [InlineData("wav", "mp3")]
    [InlineData("xlsx", "pdf")]
    [InlineData("unknown", "pdf")]
    public void Preferred_list_matches_kind(string ext, string first)
    {
        Assert.Equal(first, ConvertSuggestions.PreferredFor(ext)[0]);
    }
}

public class FileeLocatorTests
{
    [Fact]
    public void Prefers_peekos_own_copy()
    {
        var found = FileeLocator.Find(@"C:\data\filee", [@"C:\Program Files\Filee"], null, _ => true);
        Assert.Equal(@"C:\data\filee\filee-cli.exe", found);
    }

    [Fact]
    public void Falls_back_to_installed_filee()
    {
        var exists = new HashSet<string> { @"C:\Program Files\Filee\filee-cli.exe" };
        Assert.Equal(@"C:\Program Files\Filee\filee-cli.exe",
            FileeLocator.Find(@"C:\data\filee", [@"C:\Users\a\AppData\Local\Programs\Filee", @"C:\Program Files\Filee"], null, exists.Contains));
    }

    [Fact]
    public void Finds_exe_next_to_cli_folder_on_path()
    {
        var exists = new HashSet<string> { @"D:\Tools\Filee\filee-cli.exe" };
        var found = FileeLocator.Find(@"C:\data\filee", [], @"C:\Windows;""D:\Tools\Filee\cli\"";", exists.Contains);
        Assert.Equal(@"D:\Tools\Filee\filee-cli.exe", found);
    }

    [Fact]
    public void Nothing_found_gives_null()
    {
        Assert.Null(FileeLocator.Find(@"C:\data\filee", [@"C:\x"], @"C:\Windows", _ => false));
    }
}

public class FileeReleaseTests
{
    private const string Release = """
        {
          "tag_name": "v1.12.0",
          "assets": [
            { "name": "Filee-1.12.0-linux-x64-SHA256SUMS.txt", "size": 100, "browser_download_url": "https://x/linux-sums" },
            { "name": "Filee-1.12.0-linux-x64.tar.gz", "size": 1, "browser_download_url": "https://x/linux" },
            { "name": "Filee-1.12.0-SHA256SUMS.txt", "size": 900, "browser_download_url": "https://x/sums" },
            { "name": "Filee-1.12.0-win-Portable.zip", "size": 166723584, "browser_download_url": "https://x/portable" },
            { "name": "Filee-1.12.0-win-Setup.exe", "size": 1, "browser_download_url": "https://x/setup" }
          ]
        }
        """;

    [Fact]
    public void Picks_windows_portable_and_main_checksum_file()
    {
        var package = FileeRelease.ParseRelease(Release)!;
        Assert.Equal(new Version(1, 12, 0), package.Version);
        Assert.Equal("Filee-1.12.0-win-Portable.zip", package.ZipName);
        Assert.Equal("https://x/portable", package.ZipUrl);
        Assert.Equal(166723584, package.ZipSize);
        Assert.Equal("https://x/sums", package.SumsUrl);
    }

    [Fact]
    public void Release_without_portable_zip_gives_null()
    {
        Assert.Null(FileeRelease.ParseRelease("""{ "tag_name": "v2.0.0", "assets": [] }"""));
        Assert.Null(FileeRelease.ParseRelease("""{ "tag_name": "nightly", "assets": [] }"""));
    }

    [Fact]
    public void Finds_checksum_line()
    {
        const string sums = """
            1111111111111111111111111111111111111111111111111111111111111111  Filee-1.12.0-win-Setup.exe
            0396EE40CC4583264FBBB1D2C1643DDB79FCCAED2C39BE9F0F9AFED1F8A977C9 *Filee-1.12.0-win-Portable.zip
            """;
        Assert.Equal("0396ee40cc4583264fbbb1d2c1643ddb79fccaed2c39be9f0f9afed1f8a977c9",
            FileeRelease.FindChecksum(sums, "Filee-1.12.0-win-Portable.zip"));
        Assert.Null(FileeRelease.FindChecksum(sums, "lain.zip"));
        Assert.Null(FileeRelease.FindChecksum("pendek  Filee-1.12.0-win-Portable.zip", "Filee-1.12.0-win-Portable.zip"));
    }
}
