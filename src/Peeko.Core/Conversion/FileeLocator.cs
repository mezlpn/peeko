namespace Peeko.Core.Conversion;

/// <summary>
/// Mencari <c>filee-cli.exe</c>. Urutan: salinan milik Peeko (diunduh dari Pengaturan), Filee yang
/// dipasang lewat Setup (Program Files atau %LocalAppData%\Programs), lalu perintah "filee" di PATH
/// (<c>cli\filee.cmd</c> menjalankan <c>..\filee-cli.exe</c>).
/// </summary>
public static class FileeLocator
{
    public const string CliName = "filee-cli.exe";

    /// <param name="ownDir">Folder Filee milik Peeko.</param>
    /// <param name="installDirs">Folder pasang Filee yang umum.</param>
    /// <param name="pathVariable">Isi variabel PATH.</param>
    /// <param name="fileExists">Diganti saat test.</param>
    public static string? Find(string ownDir, IEnumerable<string> installDirs, string? pathVariable, Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        foreach (var candidate in Candidates(ownDir, installDirs, pathVariable))
        {
            if (fileExists(candidate)) return candidate;
        }
        return null;
    }

    private static IEnumerable<string> Candidates(string ownDir, IEnumerable<string> installDirs, string? pathVariable)
    {
        yield return Path.Combine(ownDir, CliName);
        foreach (var dir in installDirs) yield return Path.Combine(dir, CliName);

        foreach (var raw in (pathVariable ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dir = raw.Trim('"');
            if (dir.Length == 0) continue;
            yield return Path.Combine(dir, CliName);
            // Folder "cli" berisi filee.cmd; exe-nya satu folder di atasnya.
            if (Path.GetFileName(dir.TrimEnd('\\', '/')).Equals("cli", StringComparison.OrdinalIgnoreCase)
                && Path.GetDirectoryName(dir.TrimEnd('\\', '/')) is { } parent)
                yield return Path.Combine(parent, CliName);
        }
    }

    /// <summary>Lokasi pasang Filee Setup: per-pengguna dan untuk semua pengguna.</summary>
    public static IEnumerable<string> DefaultInstallDirs()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (local.Length > 0) yield return Path.Combine(local, "Programs", "Filee");
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (programs.Length > 0) yield return Path.Combine(programs, "Filee");
    }
}
