using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace Peeko.App.Settings;

/// <summary>
/// Cadangkan/pulihkan semua pengaturan dan data Peeko (file .json di %AppData%\Peeko) dalam satu zip.
/// Cache lirik, log, dan salinan file rak tidak ikut (besar dan bisa dibuat ulang).
/// </summary>
public static class BackupService
{
    private const string Marker = "peeko-backup.txt";

    public static string DefaultName => $"Peeko-cadangan-{DateTime.Now:yyyy-MM-dd}.zip";

    /// <returns>Jumlah file yang dicadangkan.</returns>
    public static int Create(string zipPath)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        int count = 0;
        foreach (var file in Directory.EnumerateFiles(App.DataDirectory, "*.json"))
        {
            zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);
            count++;
        }
        var marker = zip.CreateEntry(Marker);
        using (var w = new StreamWriter(marker.Open()))
            w.Write($"Cadangan Peeko {typeof(App).Assembly.GetName().Version?.ToString(3)} · {DateTime.Now:yyyy-MM-dd HH:mm}");
        return count;
    }

    /// <summary>Periksa isi zip: harus cadangan Peeko, dan hanya berisi file .json tanpa folder.</summary>
    public static bool IsValid(string zipPath, out int files)
    {
        files = 0;
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            if (zip.GetEntry(Marker) is null) return false;
            foreach (var e in zip.Entries)
            {
                if (e.FullName == Marker) continue;
                if (e.FullName.Contains('/') || e.FullName.Contains('\\') || !e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return false;
                files++;
            }
            return files > 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public const string RestartArg = "--pulihkan";
    private const string PendingName = "pulihkan.zip";

    /// <summary>
    /// Siapkan cadangan untuk dipulihkan, lalu jalankan ulang Peeko. Pemulihan baru terjadi saat Peeko
    /// mulai lagi (<see cref="ApplyPending"/>), supaya data di memori yang ditulis saat keluar tidak
    /// menimpa hasil pulihan.
    /// </summary>
    public static void RestoreAndRestart(string zipPath)
    {
        File.Copy(zipPath, Path.Combine(App.DataDirectory, PendingName), overwrite: true);
        if (Environment.ProcessPath is { } exe)
            Process.Start(new ProcessStartInfo(exe, RestartArg) { UseShellExecute = false })?.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>
    /// Dipanggil paling awal saat mulai. Data sekarang disimpan dulu ke "sebelum-pulih/(tanggal)"
    /// supaya tidak ada yang hilang kalau salah pilih file.
    /// </summary>
    public static void ApplyPending()
    {
        var dir = App.DataDirectory;
        var pending = Path.Combine(dir, PendingName);
        if (!File.Exists(pending)) return;
        try
        {
            if (!IsValid(pending, out _)) return;
            var keep = Path.Combine(dir, "sebelum-pulih", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(keep);
            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
                File.Copy(file, Path.Combine(keep, Path.GetFileName(file)), overwrite: true);
            using var zip = ZipFile.OpenRead(pending);
            foreach (var e in zip.Entries.Where(e => e.FullName != Marker))
                e.ExtractToFile(Path.Combine(dir, e.FullName), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
        }
        finally
        {
            try { File.Delete(pending); } catch (IOException) { }
        }
    }
}
