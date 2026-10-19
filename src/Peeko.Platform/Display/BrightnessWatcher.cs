using System.Management;

namespace Peeko.Platform.Display;

/// <summary>
/// Perubahan kecerahan layar laptop lewat WMI <c>WmiMonitorBrightnessEvent</c>.
/// Monitor eksternal tidak didukung (keputusan v1).
/// </summary>
public sealed class BrightnessWatcher : IDisposable
{
    private ManagementEventWatcher? _watcher;
    private int _last = -1;

    /// <summary>Kecerahan baru 0–100 (dari thread latar).</summary>
    public event Action<int>? Changed;

    /// <returns>false jika perangkat tidak mendukung (mis. PC desktop).</returns>
    public bool Start()
    {
        try
        {
            var scope = new ManagementScope(@"root\WMI");
            _watcher = new ManagementEventWatcher(scope, new EventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
            _watcher.EventArrived += (_, e) =>
            {
                int value = Convert.ToInt32(e.NewEvent["Brightness"]);
                if (value == _last) return;
                _last = value;
                Changed?.Invoke(value);
            };
            _watcher.Start();
            return true;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            _watcher?.Dispose();
            _watcher = null;
            return false;
        }
    }

    public void Dispose()
    {
        try { _watcher?.Stop(); } catch (ManagementException) { }
        _watcher?.Dispose();
        _watcher = null;
    }
}
