using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Peeko.Platform.Display;

/// <summary>
/// Nama aplikasi yang jendelanya sedang di depan, mis. "Google Chrome" atau "Visual Studio Code"
/// (dari keterangan file .exe-nya). Judul jendela tidak dibaca.
/// </summary>
public static class ForegroundApp
{
    private static readonly Dictionary<string, string> NameCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "ShellExperienceHost.exe", "StartMenuExperienceHost.exe", "SearchHost.exe", "SearchApp.exe",
        "LockApp.exe", "Peeko.exe", "ApplicationFrameHost.exe",
    };

    /// <summary>Null untuk desktop/taskbar/menu Start, Peeko sendiri, atau proses yang tidak bisa dibaca.</summary>
    public static string? Current()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid == Environment.ProcessId) return null;

        var path = ProcessPath(pid);
        if (path is null) return null;
        var file = Path.GetFileName(path);
        // Aplikasi Store (Kalkulator, Foto…) berjalan di dalam ApplicationFrameHost; pakai jendela anaknya.
        if (file.Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            var child = UwpChildPath(hwnd, pid);
            if (child is null) return null;
            path = child;
            file = Path.GetFileName(path);
        }
        if (Ignored.Contains(file)) return null;

        lock (NameCache)
        {
            if (NameCache.TryGetValue(path, out var cached)) return cached;
            string name;
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                name = !string.IsNullOrWhiteSpace(info.FileDescription) ? info.FileDescription.Trim()
                    : !string.IsNullOrWhiteSpace(info.ProductName) ? info.ProductName.Trim()
                    : Path.GetFileNameWithoutExtension(path);
            }
            catch (FileNotFoundException)
            {
                name = Path.GetFileNameWithoutExtension(path);
            }
            NameCache[path] = name;
            return name;
        }
    }

    private static string? UwpChildPath(IntPtr frame, uint framePid)
    {
        string? found = null;
        EnumChildWindows(frame, (child, _) =>
        {
            GetWindowThreadProcessId(child, out uint pid);
            if (pid != framePid && ProcessPath(pid) is { } p)
            {
                found = p;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private static string? ProcessPath(uint pid)
    {
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(handle, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private delegate bool EnumProc(IntPtr hwnd, IntPtr param);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr param);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);
}
