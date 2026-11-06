using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Peeko.Platform.Network;

/// <summary>
/// Mode pesawat Windows. Tidak ada API publiknya, tetapi Windows menyimpan statusnya di
/// HKLM\…\RadioManagement\SystemRadioState (nilai bawaan 1 = pesawat). Perubahan ditunggu dengan
/// RegNotifyChangeKeyValue di thread sendiri, jadi tidak ada polling.
/// </summary>
public sealed class AirplaneWatcher : IDisposable
{
    private const string KeyPath = @"SYSTEM\CurrentControlSet\Control\RadioManagement\SystemRadioState";
    private const int REG_NOTIFY_CHANGE_LAST_SET = 0x4;

    private readonly ManualResetEvent _stop = new(false);
    private Thread? _thread;
    private bool _on;

    /// <summary>Mode pesawat berubah: aktif?</summary>
    public event Action<bool>? Changed;

    public static bool? Read()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
            return key?.GetValue(null) is int v ? v == 1 : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    public bool Start()
    {
        if (Read() is not { } initial) return false;
        _on = initial;
        _thread = new Thread(Loop) { IsBackground = true, Name = "Peeko airplane watcher" };
        _thread.Start();
        return true;
    }

    private void Loop()
    {
        using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
        if (key is null) return;
        using var changed = new AutoResetEvent(false);
        var handles = new WaitHandle[] { _stop, changed };
        while (true)
        {
            int rc = RegNotifyChangeKeyValue(key.Handle.DangerousGetHandle(), false, REG_NOTIFY_CHANGE_LAST_SET,
                changed.SafeWaitHandle.DangerousGetHandle(), true);
            if (rc != 0) return;
            if (WaitHandle.WaitAny(handles) == 0) return;
            if (Read() is { } on && on != _on)
            {
                _on = on;
                Changed?.Invoke(on);
            }
        }
    }

    public void Dispose()
    {
        _stop.Set();
        _thread?.Join(500);
        _stop.Dispose();
    }

    [DllImport("advapi32.dll")]
    private static extern int RegNotifyChangeKeyValue(IntPtr hKey, bool watchSubtree, int notifyFilter, IntPtr hEvent, bool asynchronous);
}
