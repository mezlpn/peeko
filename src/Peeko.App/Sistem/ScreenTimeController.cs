using System.Windows.Threading;
using Microsoft.Win32;
using Peeko.App.Productivity;
using Peeko.App.Settings;
using Peeko.Core.Status;
using Peeko.Core.Storage;
using Peeko.Platform.Display;
using Peeko.Platform.Input;

namespace Peeko.App.Sistem;

/// <summary>
/// Screen time (mati dari awal). Berbasis event: pergantian jendela di depan dan kunci/buka layar.
/// Selama aktif ada satu timer per menit untuk mengecek apakah kamu meninggalkan laptop (tidak ada
/// input ≥ 5 menit) dan menyimpan hitungan; waktu ditinggal tidak dihitung.
/// </summary>
public sealed class ScreenTimeController : IDisposable
{
    private static readonly TimeSpan Away = TimeSpan.FromMinutes(5);

    private readonly SettingsService _settings;
    private readonly JsonFileStore<ScreenTimeData> _store;
    private readonly AutoSaver _saver;
    private readonly DispatcherTimer _minute = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMinutes(1) };
    private bool _enabled;
    private bool _locked;

    public ScreenTimeController(SettingsService settings)
    {
        _settings = settings;
        _store = new JsonFileStore<ScreenTimeData>(App.DataDirectory, "screentime.json");
        Log = new ScreenTime(_store.Load());
        _saver = new AutoSaver(() => _store.Save(Log.Data));
        Log.Changed += () =>
        {
            _saver.MarkDirty();
            Updated?.Invoke();
        };
        _minute.Tick += (_, _) => Tick();
    }

    public ScreenTime Log { get; }

    public bool Enabled => _enabled;

    public event Action? Updated;

    public void Apply()
    {
        bool want = _settings.Current.ScreenTimeEnabled;
        if (want == _enabled) return;
        _enabled = want;
        if (want)
        {
            SystemEvents.SessionSwitch += OnSessionSwitch;
            _minute.Start();
            Log.Switch(ForegroundApp.Current(), DateTime.Now);
        }
        else
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _minute.Stop();
            Log.Pause(DateTime.Now);
            _saver.Flush();
        }
        Updated?.Invoke();
    }

    /// <summary>Jendela di depan berganti (dari ForegroundWindowWatcher, di thread UI).</summary>
    public void OnForegroundChanged()
    {
        if (!_enabled || _locked) return;
        Log.Switch(ForegroundApp.Current(), DateTime.Now);
    }

    /// <summary>Catat waktu berjalan sebelum ditampilkan.</summary>
    public void Flush()
    {
        if (_enabled && !_locked) Log.Flush(DateTime.Now);
    }

    private void Tick()
    {
        if (_locked) return;
        var idle = IdleTime.Get();
        var now = DateTime.Now;
        if (idle >= Away)
        {
            if (Log.Current is not null) Log.Pause(now - idle);
            return;
        }
        // Kembali setelah ditinggal: mulai lagi dari aplikasi yang sedang di depan.
        if (Log.Current is null) Log.Switch(ForegroundApp.Current(), now);
        else Log.Flush(now);
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        bool lockedNow = e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect;
        bool unlocked = e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect;
        if (!lockedNow && !unlocked) return;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _locked = lockedNow;
            if (lockedNow) Log.Pause(DateTime.Now);
            else Log.Switch(ForegroundApp.Current(), DateTime.Now);
        });
    }

    public void ClearHistory()
    {
        Log.Clear();
        if (_enabled && !_locked) Log.Switch(ForegroundApp.Current(), DateTime.Now);
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _minute.Stop();
        if (_enabled) Log.Pause(DateTime.Now);
        _saver.Flush();
    }
}
