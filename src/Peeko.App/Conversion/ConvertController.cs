using System.IO;
using System.Net.Http;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Peeko.App.Productivity;
using Peeko.Core.Conversion;
using Peeko.Core.Peeks;

namespace Peeko.App.Conversion;

public enum ConvertStage
{
    /// <summary>Tidak ada yang sedang diubah; Rak tampil seperti biasa.</summary>
    Idle,
    /// <summary>Menanyakan format tujuan ke Filee.</summary>
    Loading,
    /// <summary>Pilih format tujuan.</summary>
    Choose,
    /// <summary>Filee belum ada: tawarkan pemasangan.</summary>
    NeedsInstall,
    Installing,
    Converting,
    Done,
    Failed,
}

/// <summary>
/// Alur "Ubah format" di tab Rak: file dijatuhkan di zona Ubah format (atau tombol di kartu rak) →
/// pilih format → Filee mengonversi → hasilnya masuk rak dan muncul peek.
/// </summary>
public sealed class ConvertController
{
    private static readonly TimeSpan DoneLinger = TimeSpan.FromSeconds(2.5);

    private readonly FileeService _filee;
    private readonly ListsController _lists;
    private readonly ILogger<ConvertController> _log;
    private readonly DispatcherTimer _closeTimer = new() { Interval = DoneLinger };
    private CancellationTokenSource? _work;
    private CancellationTokenSource? _install;
    private int _session;

    public ConvertController(FileeService filee, ListsController lists, ILogger<ConvertController> log)
    {
        _filee = filee;
        _lists = lists;
        _log = log;
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            if (Stage == ConvertStage.Done) Close();
        };
    }

    public FileeService Filee => _filee;

    public ConvertStage Stage { get; private set; }

    public IReadOnlyList<string> Files { get; private set; } = [];

    /// <summary>4–5 format yang paling sering dipakai untuk file ini.</summary>
    public IReadOnlyList<string> Common { get; private set; } = [];

    /// <summary>Semua format yang bisa.</summary>
    public IReadOnlyList<string> All { get; private set; } = [];

    /// <summary>Format yang baru bisa setelah mesin tambahan Filee dipasang (mis. FFmpeg).</summary>
    public IReadOnlyList<string> NeedsEngine { get; private set; } = [];

    public string Target { get; private set; } = "";

    public IReadOnlyList<string> Outputs { get; private set; } = [];

    /// <summary>Progres unduhan Filee 0–1.</summary>
    public double InstallProgress { get; private set; }

    public bool Installing => _install is not null;

    public string? Message { get; private set; }

    /// <summary>Gagal karena format itu butuh mesin tambahan: tampilkan tombol "Buka Filee".</summary>
    public bool CanOpenEngines { get; private set; }

    public event Action? Changed;

    public event Action<PeekRequest>? PeekRequested;

    /// <summary>"foto.jpg" atau "3 file".</summary>
    public string FilesLabel => Files.Count == 1 ? Path.GetFileName(Files[0]) : $"{Files.Count} file";

    /// <summary>Mulai mengubah file ini (folder diabaikan).</summary>
    public async void Begin(IEnumerable<string> paths)
    {
        _closeTimer.Stop();
        _work?.Cancel();
        int session = ++_session;
        Files = paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Common = All = NeedsEngine = Outputs = [];
        CanOpenEngines = false;
        Message = null;

        if (Files.Count == 0)
        {
            Fail("Pilih file, bukan folder.");
            return;
        }
        if (!_filee.IsReady)
        {
            Set(Installing ? ConvertStage.Installing : ConvertStage.NeedsInstall);
            return;
        }

        Set(ConvertStage.Loading);
        try
        {
            var extensions = Files.Select(f => ConvertSuggestions.Normalize(Path.GetExtension(f))).Distinct().ToList();
            var lookups = await Task.WhenAll(extensions.Select(e => _filee.GetTargetsAsync(e, CancellationToken.None)));
            if (session != _session) return;

            if (lookups.Any(t => t is null))
            {
                Fail(extensions.Count == 1 ? $"Format .{extensions[0]} belum didukung Filee." : "Ada format file yang belum didukung Filee.");
                return;
            }
            var targets = lookups.Select(t => t!).ToList();
            All = ConvertSuggestions.Intersect(targets.Select(t => t.Ready).ToList());
            NeedsEngine = ConvertSuggestions.Intersect(targets.Select(t => (IReadOnlyList<string>)[.. t.Ready, .. t.NeedsEngine]).ToList())
                .Except(All, StringComparer.OrdinalIgnoreCase).ToList();
            Common = ConvertSuggestions.Pick(All, extensions);

            if (All.Count == 0)
            {
                CanOpenEngines = NeedsEngine.Count > 0 && _filee.AppPath is not null;
                Fail(NeedsEngine.Count > 0
                    ? "Butuh mesin tambahan Filee (mis. FFmpeg untuk video & audio)."
                    : "Tidak ada format tujuan untuk file ini.");
                return;
            }
            Set(ConvertStage.Choose);
        }
        catch (FileeException ex)
        {
            if (session == _session) Fail(ex.Message);
        }
    }

    /// <summary>Ubah ke format yang dipilih.</summary>
    public async void Choose(string target)
    {
        if (Stage != ConvertStage.Choose || Files.Count == 0) return;
        int session = ++_session;
        Target = target;
        Set(ConvertStage.Converting);
        _work = new CancellationTokenSource();
        try
        {
            var result = await _filee.ConvertAsync(Files, target, _work.Token);
            if (session != _session) return;
            Outputs = result.Outputs.Where(File.Exists).ToList();
            _log.LogInformation("Konversi ke {Target}: {Converted} berhasil, {Failed} gagal", target, result.Converted, result.Failed);

            if (Outputs.Count == 0)
            {
                Fail(Friendly(result.FirstError) ?? "Filee tidak menghasilkan file.");
                return;
            }

            _lists.Shelf.Add(Outputs);
            Message = result.Failed > 0 ? $"{result.Failed} file gagal" : null;
            Set(ConvertStage.Done);
            PeekRequested?.Invoke(new PeekRequest(PeekKind.ConvertDone)
            {
                Text = Outputs.Count == 1 ? Path.GetFileName(Outputs[0]) : $"{Outputs.Count} file siap",
                Detail = "masuk rak",
            });
            _closeTimer.Start();
        }
        catch (FileeException ex)
        {
            if (session == _session) Fail(Friendly(ex.Message) ?? ex.Message, peek: true);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _work?.Dispose();
            _work = null;
        }
    }

    /// <summary>Unduh dan pasang Filee (bisa dari panel notch atau Pengaturan).</summary>
    public async Task InstallAsync()
    {
        if (_install is not null) return;
        using var install = _install = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        InstallProgress = 0;
        bool fromPanel = Stage is ConvertStage.NeedsInstall;
        if (fromPanel) Set(ConvertStage.Installing);
        else Changed?.Invoke();

        var progress = new Progress<double>(p =>
        {
            InstallProgress = p;
            Changed?.Invoke();
        });
        try
        {
            var version = await _filee.InstallAsync(progress, install.Token);
            _install = null;
            PeekRequested?.Invoke(new PeekRequest(PeekKind.ConvertReady) { Text = version });
            // Lanjutkan file yang sedang menunggu.
            if (Stage == ConvertStage.Installing && Files.Count > 0) Begin(Files);
            else Changed?.Invoke();
        }
        catch (Exception ex) when (ex is FileeException or HttpRequestException or IOException or UnauthorizedAccessException
                                       or InvalidDataException or OperationCanceledException)
        {
            _log.LogWarning(ex, "Gagal memasang Filee");
            _install = null;
            var message = ex switch
            {
                FileeException f => f.Message,
                HttpRequestException or OperationCanceledException => "Unduhan gagal. Periksa internet, lalu coba lagi.",
                _ => "Gagal memasang Filee. Coba lagi.",
            };
            if (Stage == ConvertStage.Installing) Fail(message);
            else
            {
                Message = message;
                Changed?.Invoke();
            }
        }
    }

    public async Task UninstallAsync()
    {
        try
        {
            await _filee.UninstallAsync();
            Message = null;
        }
        catch (FileeException ex)
        {
            Message = ex.Message;
        }
        Changed?.Invoke();
    }

    /// <summary>Buka Filee untuk memasang mesin tambahan (FFmpeg, LibreOffice, …).</summary>
    public void OpenEngines() => _filee.OpenApp();

    public void Close()
    {
        _closeTimer.Stop();
        _work?.Cancel();
        _session++;
        Files = Outputs = [];
        Message = null;
        // Pemasangan tetap berjalan di latar walau panel ditutup.
        Set(ConvertStage.Idle);
    }

    private void Fail(string message, bool peek = false)
    {
        Message = message;
        Set(ConvertStage.Failed);
        if (peek) PeekRequested?.Invoke(new PeekRequest(PeekKind.ConvertFailed) { Text = message });
    }

    private void Set(ConvertStage stage)
    {
        Stage = stage;
        Changed?.Invoke();
    }

    /// <summary>Pesan Filee (bahasa Inggris) yang sering muncul, dalam bahasa Indonesia.</summary>
    private static string? Friendly(string? message) => message switch
    {
        null or "" => null,
        var m when m.StartsWith("No engine can do this conversion", StringComparison.OrdinalIgnoreCase)
            => "Butuh mesin tambahan Filee untuk format ini.",
        var m when m.StartsWith("not found", StringComparison.OrdinalIgnoreCase) => "File tidak ditemukan.",
        var m => m,
    };
}
