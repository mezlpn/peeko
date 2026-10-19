using Microsoft.Win32;

namespace Peeko.Platform.Display;

/// <summary>"Mulai bersama Windows" lewat HKCU\...\Run (tanpa hak admin).</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Peeko";

    public static bool IsEnabled(string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value
            && value.Contains(exePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Hapus entri dari nama lama aplikasi (mis. "Celah" sebelum berganti nama menjadi Peeko).</summary>
    /// <returns>true jika entri lama ada (berarti pengguna sebelumnya menyalakan startup).</returns>
    public static bool RemoveLegacy(string legacyName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(legacyName) is null) return false;
        key.DeleteValue(legacyName);
        return true;
    }

    public static void Set(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{exePath}\"");
        else if (key.GetValue(ValueName) is not null)
            key.DeleteValue(ValueName);
    }
}
