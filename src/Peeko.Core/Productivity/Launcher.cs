namespace Peeko.Core.Productivity;

public sealed class LauncherApp
{
    public string Name { get; set; } = "";
    /// <summary>Path .exe, pintasan .lnk, file, atau folder.</summary>
    public string Path { get; set; } = "";
}

public sealed class LauncherData
{
    public List<LauncherApp> Apps { get; set; } = [];
}

/// <summary>Daftar aplikasi favorit di tab Aplikasi (maksimal <see cref="Max"/>, urutan sesuai ditambahkan).</summary>
public sealed class Launcher
{
    public const int Max = 12;

    private readonly LauncherData _d;

    public Launcher(LauncherData? restored = null)
    {
        _d = restored ?? new LauncherData();
        _d.Apps.RemoveAll(a => string.IsNullOrWhiteSpace(a.Path));
        if (_d.Apps.Count > Max) _d.Apps.RemoveRange(Max, _d.Apps.Count - Max);
    }

    public LauncherData Data => _d;

    public IReadOnlyList<LauncherApp> Apps => _d.Apps;

    public bool IsFull => _d.Apps.Count >= Max;

    public event Action? Changed;

    /// <returns>false jika sudah ada atau daftar penuh.</returns>
    public bool Add(string path, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(path) || IsFull) return false;
        if (_d.Apps.Any(a => string.Equals(a.Path, path, StringComparison.OrdinalIgnoreCase))) return false;
        _d.Apps.Add(new LauncherApp { Path = path, Name = string.IsNullOrWhiteSpace(name) ? DisplayName(path) : name.Trim() });
        Changed?.Invoke();
        return true;
    }

    public void Remove(LauncherApp app)
    {
        if (_d.Apps.Remove(app)) Changed?.Invoke();
    }

    /// <summary>Geser ke kiri (−1) atau kanan (+1).</summary>
    public void Move(LauncherApp app, int step)
    {
        int i = _d.Apps.IndexOf(app);
        int j = i + step;
        if (i < 0 || j < 0 || j >= _d.Apps.Count) return;
        (_d.Apps[i], _d.Apps[j]) = (_d.Apps[j], _d.Apps[i]);
        Changed?.Invoke();
    }

    /// <summary>"C:\…\Visual Studio Code.lnk" → "Visual Studio Code"; folder → nama folder.</summary>
    public static string DisplayName(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        var name = System.IO.Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(name)) return trimmed; // akar drive, mis. "D:"
        var ext = System.IO.Path.GetExtension(name);
        return ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".url", StringComparison.OrdinalIgnoreCase)
            ? System.IO.Path.GetFileNameWithoutExtension(name)
            : name;
    }
}
