using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Peeko.Core.Updates;

namespace Peeko.App.Updates;

public enum UpdateState
{
    None,
    Available,
    Downloading,
    Installing,
    Failed,
}

/// <summary>
/// Update otomatis dari GitHub Releases. Dicek 10 dtk setelah start lalu tiap 6 jam. "Pasang"
/// mengunduh zip, mengganti Peeko.exe (exe yang berjalan cukup diganti nama), lalu menyalakan
/// versi baru. Gagal di tengah jalan → exe lama dikembalikan.
/// </summary>
public sealed class UpdateController : IDisposable
{
    public const string UpdatedArg = "--updated";
    private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly ILogger<UpdateController> _log;
    private readonly HttpClient _http;
    private readonly UpdateChecker _checker;
    private readonly DispatcherTimer _timer = new() { Interval = FirstCheck };
    private CancellationTokenSource? _download;

    public UpdateController(ILogger<UpdateController> log, string? feedOverride = null)
    {
        _log = log;
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Peeko/{CurrentVersion.ToString(3)} (aplikasi notch Windows)");
        _checker = new UpdateChecker(_http, feedOverride);
        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = Interval;
            await CheckAsync();
        };
    }

    public static Version CurrentVersion => typeof(App).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public UpdateState State { get; private set; }

    public ReleaseInfo? Latest { get; private set; }

    /// <summary>Progres unduhan 0–1.</summary>
    public double Progress { get; private set; }

    public string? Error { get; private set; }

    /// <summary>Status/progres berubah.</summary>
    public event Action? Changed;

    /// <summary>Versi baru baru saja ditemukan (sekali per versi).</summary>
    public event Action<ReleaseInfo>? Found;

    private static string? ExePath => Environment.ProcessPath;

    /// <summary>
    /// Bisa memasang sendiri hanya jika berjalan sebagai Peeko.exe satu-file (versi rilis).
    /// Saat dijalankan dari kode (dotnet run), tombol membuka halaman rilis.
    /// </summary>
    public static bool CanSelfUpdate =>
        string.IsNullOrEmpty(typeof(App).Assembly.Location)
        && ExePath is { } exe
        && Path.GetFileName(exe).Equals("Peeko.exe", StringComparison.OrdinalIgnoreCase);

    public void Start()
    {
        CleanupPreviousUpdate();
        _timer.Start();
    }

    public async Task CheckAsync()
    {
        if (State is UpdateState.Downloading or UpdateState.Installing) return;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var latest = await _checker.GetLatestAsync(cts.Token);
            if (latest is null || !UpdateChecker.IsNewer(CurrentVersion, latest.Version))
            {
                _log.LogDebug("Sudah versi terbaru ({Version})", CurrentVersion.ToString(3));
                return;
            }

            bool isNew = Latest?.Version != latest.Version;
            Latest = latest;
            if (State != UpdateState.Failed) State = UpdateState.Available;
            _log.LogInformation("Versi baru tersedia: {Version}", latest.Version);
            Changed?.Invoke();
            if (isNew) Found?.Invoke(latest);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // Offline atau GitHub sedang bermasalah: coba lagi di jadwal berikutnya.
            _log.LogDebug("Cek update gagal: {Message}", ex.Message);
        }
    }

    /// <summary>Unduh, pasang, dan nyalakan ulang. Mengembalikan false jika gagal (lihat <see cref="Error"/>).</summary>
    public async Task<bool> InstallAsync()
    {
        if (Latest is not { } release || State is UpdateState.Downloading or UpdateState.Installing) return false;

        if (!CanSelfUpdate)
        {
            OpenReleasePage();
            return false;
        }

        var exe = ExePath!;
        var dir = Path.GetDirectoryName(exe)!;
        var newExe = Path.Combine(dir, "Peeko.new.exe");
        var oldExe = Path.Combine(dir, "Peeko.old.exe");
        var zipPath = Path.Combine(Path.GetTempPath(), "Peeko-update", release.AssetName);

        try
        {
            // 1. Unduh
            SetState(UpdateState.Downloading, 0);
            _download = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
            await DownloadAsync(release, zipPath, _download.Token);

            // 2. Ambil Peeko.exe dari zip, periksa bahwa itu memang program Windows.
            SetState(UpdateState.Installing, 1);
            await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(zipPath);
                var entry = zip.Entries.FirstOrDefault(e => e.Name.Equals("Peeko.exe", StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException("Zip rilis tidak berisi Peeko.exe.");
                entry.ExtractToFile(newExe, overwrite: true);
            });
            ValidateExecutable(newExe);

            // 3. Tukar: exe yang sedang berjalan boleh diganti nama, lalu exe baru mengambil tempatnya.
            if (File.Exists(oldExe)) File.Delete(oldExe);
            File.Move(exe, oldExe);
            try
            {
                File.Move(newExe, exe);
            }
            catch
            {
                File.Move(oldExe, exe); // kembalikan
                throw;
            }

            // 4. Nyalakan versi baru; ia menunggu proses ini keluar (lihat App.OnStartup).
            _log.LogInformation("Update dipasang: {From} → {To}", CurrentVersion.ToString(3), release.Version);
            Process.Start(new ProcessStartInfo(exe, $"{UpdatedArg}={CurrentVersion.ToString(3)}") { UseShellExecute = false, WorkingDirectory = dir });
            System.Windows.Application.Current.Shutdown();
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException
                                       or InvalidDataException or TaskCanceledException or System.ComponentModel.Win32Exception)
        {
            _log.LogWarning(ex, "Update gagal");
            Error = ex is UnauthorizedAccessException
                ? "Folder Peeko tidak bisa ditulis. Pindahkan Peeko ke Documents, atau unduh manual."
                : "Update gagal. Periksa internet, lalu coba lagi.";
            TryDelete(newExe);
            SetState(UpdateState.Failed, 0);
            return false;
        }
        finally
        {
            _download?.Dispose();
            _download = null;
            TryDelete(zipPath);
        }
    }

    public void OpenReleasePage()
    {
        var url = string.IsNullOrEmpty(Latest?.PageUrl) ? "https://github.com/mezlpn/peeko/releases/latest" : Latest.PageUrl;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _log.LogWarning(ex, "Gagal membuka halaman rilis");
        }
    }

    private async Task DownloadAsync(ReleaseInfo release, string target, CancellationToken ct)
    {
        using var response = await _http.GetAsync(release.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? release.AssetSize;

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(target);
        var buffer = new byte[81920];
        long read = 0;
        var lastReport = DateTime.MinValue;
        int n;
        while ((n = await source.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            // Laporkan progres paling sering 5× per detik.
            if (total > 0 && DateTime.UtcNow - lastReport > TimeSpan.FromMilliseconds(200))
            {
                lastReport = DateTime.UtcNow;
                SetState(UpdateState.Downloading, Math.Min(1, (double)read / total));
            }
        }
    }

    /// <summary>Exe Windows yang sah diawali "MZ" dan tidak mungkin sekecil ini.</summary>
    private static void ValidateExecutable(string path)
    {
        var info = new FileInfo(path);
        if (info.Length < 1_000_000) throw new InvalidDataException("Peeko.exe di zip terlalu kecil.");
        using var stream = File.OpenRead(path);
        if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z') throw new InvalidDataException("Peeko.exe di zip bukan program Windows.");
    }

    /// <summary>Sisa update sebelumnya: Peeko.old.exe baru bisa dihapus setelah proses lamanya keluar.</summary>
    private void CleanupPreviousUpdate()
    {
        if (ExePath is not { } exe) return;
        var dir = Path.GetDirectoryName(exe)!;
        TryDelete(Path.Combine(dir, "Peeko.old.exe"));
        TryDelete(Path.Combine(dir, "Peeko.new.exe"));
    }

    private void SetState(UpdateState state, double progress)
    {
        State = state;
        Progress = progress;
        if (state != UpdateState.Failed) Error = null;
        Changed?.Invoke();
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

    public void Dispose()
    {
        _timer.Stop();
        _download?.Cancel();
        _http.Dispose();
    }
}
