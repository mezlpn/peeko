using System.Diagnostics;
using System.IO;
using Peeko.Core.Productivity;

namespace Peeko.App.Sistem;

/// <summary>
/// "Buka Spotify" dari asisten AI: dicari di aplikasi favorit lalu di pintasan menu Start. Hanya
/// pintasan yang sudah terpasang yang bisa dibuka; teks dari AI tidak pernah dijalankan sebagai perintah.
/// </summary>
public static class AppFinder
{
    public static bool Open(string name, Launcher favorites)
    {
        var target = Find(name.Trim(), favorites);
        if (target is null) return false;
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    private static string? Find(string name, Launcher favorites)
    {
        if (name.Length < 2) return null;
        var fav = favorites.Apps.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                  ?? favorites.Apps.FirstOrDefault(a => a.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (fav is not null) return fav.Path;

        var shortcuts = new[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.CommonPrograms }
            .Select(Environment.GetFolderPath)
            .Where(Directory.Exists)
            .SelectMany(d =>
            {
                try
                {
                    return Directory.EnumerateFiles(d, "*.lnk", SearchOption.AllDirectories);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return [];
                }
            })
            .Where(p => !Path.GetFileNameWithoutExtension(p).Contains("uninstall", StringComparison.OrdinalIgnoreCase))
            .ToList();
        return shortcuts.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).Equals(name, StringComparison.OrdinalIgnoreCase))
               ?? shortcuts.OrderBy(p => Path.GetFileNameWithoutExtension(p).Length)
                   .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).Contains(name, StringComparison.OrdinalIgnoreCase));
    }
}
