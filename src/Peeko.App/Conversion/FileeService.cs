using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Peeko.App.Updates;
using Peeko.Core.Conversion;

namespace Peeko.App.Conversion;

/// <summary>Filee menolak permintaan (argumen salah, file hilang) atau tidak bisa dijalankan.</summary>
public sealed class FileeException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Menjalankan <c>filee-cli</c> sebagai proses terpisah: Peeko tetap ringan, dan Filee hanya hidup
/// selama ada yang dikonversi. Juga memasang/menghapus salinan Filee milik Peeko di
/// %LocalAppData%\Peeko\filee (paket portable resmi, dicek SHA-256).
/// </summary>
public sealed class FileeService : IDisposable
{
    private static readonly TimeSpan ConvertTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<FileeService> _log;
    private readonly HttpClient _http;
    private readonly FileeRelease _release;
    private readonly ConcurrentDictionary<string, FormatTargets?> _formats = new(StringComparer.OrdinalIgnoreCase);

    public FileeService(ILogger<FileeService> log)
    {
        _log = log;
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Peeko/{UpdateController.CurrentVersion.ToString(3)} (aplikasi notch Windows)");
        _release = new FileeRelease(_http);
        Locate();
    }

    /// <summary>Folder Filee milik Peeko (besar, jadi di Local, bukan Roaming).</summary>
    public static string OwnDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Peeko", "filee");

    public string? CliPath { get; private set; }

    public bool IsReady => CliPath is not null;

    /// <summary>Filee yang dipakai adalah salinan milik Peeko (boleh dihapus dari Pengaturan).</summary>
    public bool IsOwnCopy => CliPath is { } p && p.StartsWith(OwnDirectory, StringComparison.OrdinalIgnoreCase);

    /// <summary>"1.12.0".</summary>
    public string? Version
    {
        get
        {
            if (CliPath is null) return null;
            var raw = FileVersionInfo.GetVersionInfo(CliPath).ProductVersion ?? "";
            int plus = raw.IndexOf('+');
            return plus > 0 ? raw[..plus] : raw;
        }
    }

    /// <summary>Aplikasi Filee (jendela) di sebelah filee-cli: untuk memasang mesin tambahan seperti FFmpeg.</summary>
    public string? AppPath => CliPath is { } cli && Path.Combine(Path.GetDirectoryName(cli)!, "Filee.exe") is var app && File.Exists(app) ? app : null;

    public void Locate()
    {
        CliPath = FileeLocator.Find(OwnDirectory, FileeLocator.DefaultInstallDirs(), Environment.GetEnvironmentVariable("PATH"));
        _formats.Clear();
        _log.LogInformation("Filee: {Path}", CliPath ?? "tidak ditemukan");
    }

    // ── Menjalankan filee-cli ─────────────────────────────────────────────────

    /// <summary>Format tujuan untuk satu ekstensi (di-cache). null jika Filee tidak mengenalnya.</summary>
    public async Task<FormatTargets?> GetTargetsAsync(string extension, CancellationToken ct)
    {
        var ext = ConvertSuggestions.Normalize(extension);
        if (ext.Length == 0) return null;
        if (_formats.TryGetValue(ext, out var cached)) return cached;

        var (code, stdout, _) = await RunAsync(["formats", ext], QueryTimeout, ct);
        var targets = code == 0 ? FileeOutput.ParseFormats(stdout) : null;
        _formats[ext] = targets;
        return targets;
    }

    /// <summary>Ubah file ke <paramref name="target"/>. Hasil disimpan di sebelah file aslinya.</summary>
    public async Task<ConvertResult> ConvertAsync(IReadOnlyList<string> files, string target, CancellationToken ct)
    {
        var args = new List<string> { "convert" };
        args.AddRange(files);
        args.AddRange(["--to", target, "--json"]);
        var (code, stdout, stderr) = await RunAsync(args, ConvertTimeout, ct);
        try
        {
            return FileeOutput.ParseResult(stdout);
        }
        catch (System.Text.Json.JsonException)
        {
            // Kode 2/3: argumen salah, file hilang, atau tidak ada yang bisa dikonversi; pesannya di stderr.
            var message = FirstLine(stderr) ?? FirstLine(stdout) ?? $"Filee berhenti dengan kode {code}.";
            throw new FileeException(message.StartsWith("filee: ") ? message["filee: ".Length..] : message);
        }
    }

    private async Task<(int Code, string Stdout, string Stderr)> RunAsync(IEnumerable<string> args, TimeSpan timeout, CancellationToken ct)
    {
        var cli = CliPath ?? throw new FileeException("Filee belum dipasang.");
        var psi = new ProcessStartInfo(cli)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(cli)!,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new FileeException("Filee tidak bisa dijalankan.", ex);
        }
        // Proses latar: jangan berebut CPU dengan aplikasi yang sedang dipakai.
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (InvalidOperationException) { }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout);
        var stdout = process.StandardOutput.ReadToEndAsync(limit.Token);
        var stderr = process.StandardError.ReadToEndAsync(limit.Token);
        try
        {
            await process.WaitForExitAsync(limit.Token);
            return (process.ExitCode, await stdout, await stderr);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (ct.IsCancellationRequested) throw;
            throw new FileeException("Filee terlalu lama, konversi dihentikan.");
        }
    }

    private static string? FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

    // ── Pasang / hapus ────────────────────────────────────────────────────────

    /// <summary>
    /// Unduh paket portable Filee terbaru, cocokkan SHA-256 dengan daftar resmi rilisnya, lalu ekstrak.
    /// Folder lama baru diganti setelah ekstraksi selesai, jadi gagal di tengah tidak merusak salinan lama.
    /// </summary>
    /// <param name="progress">0–1 selama mengunduh; 1 saat mengekstrak.</param>
    public async Task<string> InstallAsync(IProgress<double> progress, CancellationToken ct)
    {
        var package = await _release.GetLatestAsync(ct) ?? throw new FileeException("Paket Filee untuk Windows tidak ditemukan.");
        var sums = await _http.GetStringAsync(package.SumsUrl, ct);
        var expected = FileeRelease.FindChecksum(sums, package.ZipName)
            ?? throw new FileeException("Daftar checksum Filee tidak memuat paketnya.");

        var tempDir = Path.Combine(Path.GetTempPath(), "Peeko-filee");
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, package.ZipName);
        var staging = OwnDirectory + ".new";
        var previous = OwnDirectory + ".old";
        try
        {
            var actual = await DownloadAsync(package, zipPath, progress, ct);
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                _log.LogWarning("Checksum Filee tidak cocok: {Actual} ≠ {Expected}", actual, expected);
                throw new FileeException("File unduhan rusak (checksum tidak cocok). Coba lagi.");
            }

            progress.Report(1);
            await Task.Run(() =>
            {
                DeleteDirectory(staging);
                ZipFile.ExtractToDirectory(zipPath, staging);
                // Simbol debug (±110 MB) tidak diperlukan untuk menjalankan Filee.
                foreach (var pdb in Directory.EnumerateFiles(staging, "*.pdb", SearchOption.AllDirectories)) File.Delete(pdb);
                if (!File.Exists(Path.Combine(staging, FileeLocator.CliName)))
                    throw new FileeException("Paket Filee tidak berisi filee-cli.exe.");

                DeleteDirectory(previous);
                if (Directory.Exists(OwnDirectory)) Directory.Move(OwnDirectory, previous);
                Directory.Move(staging, OwnDirectory);
                DeleteDirectory(previous);
            }, ct);

            _log.LogInformation("Filee {Version} dipasang di {Dir}", package.Version, OwnDirectory);
            Locate();
            return package.Version.ToString(3);
        }
        finally
        {
            TryDelete(zipPath);
            DeleteDirectory(staging, quiet: true);
        }
    }

    /// <summary>Unduh sambil menghitung SHA-256; mengembalikan hash huruf kecil.</summary>
    private async Task<string> DownloadAsync(FileePackage package, string target, IProgress<double> progress, CancellationToken ct)
    {
        using var response = await _http.GetAsync(package.ZipUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? package.ZipSize;

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(target);
        var buffer = new byte[81920];
        long read = 0;
        var lastReport = DateTime.MinValue;
        int n;
        while ((n = await source.ReadAsync(buffer, ct)) > 0)
        {
            sha.AppendData(buffer, 0, n);
            await file.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            if (total > 0 && DateTime.UtcNow - lastReport > TimeSpan.FromMilliseconds(200))
            {
                lastReport = DateTime.UtcNow;
                progress.Report(Math.Min(0.99, (double)read / total));
            }
        }
        return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>Hapus salinan Filee milik Peeko. Filee yang dipasang sendiri oleh pengguna tidak disentuh.</summary>
    public async Task UninstallAsync()
    {
        await Task.Run(() => DeleteDirectory(OwnDirectory));
        _log.LogInformation("Salinan Filee dihapus");
        Locate();
    }

    /// <summary>Buka aplikasi Filee, mis. untuk memasang FFmpeg (Settings → Engines di Filee).</summary>
    public bool OpenApp()
    {
        if (AppPath is not { } app) return false;
        try
        {
            Process.Start(new ProcessStartInfo(app) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(app)! });
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _log.LogWarning(ex, "Gagal membuka Filee");
            return false;
        }
    }

    private void DeleteDirectory(string path, bool quiet = false)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!quiet) throw new FileeException("Folder Filee sedang dipakai. Tutup Filee lalu coba lagi.", ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Dispose() => _http.Dispose();
}
