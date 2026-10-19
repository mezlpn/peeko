using System.Windows.Threading;
using Peeko.Core.Peeks;
using Peeko.Core.Productivity;
using Peeko.Core.Storage;
using Microsoft.Win32;

namespace Peeko.App.Productivity;

/// <summary>
/// Timer fokus (Pomodoro). Satu timer sekali-jalan tepat di akhir sesi; tick 1 dtk untuk tampilan
/// hanya berjalan saat timernya terlihat (pil atau modul Fokus).
/// </summary>
public sealed class FocusController : IDisposable
{
    private readonly JsonFileStore<FocusSnapshot> _store;
    private readonly AutoSaver _saver;
    private readonly DispatcherTimer _completion = new(DispatcherPriority.Normal);
    private readonly DispatcherTimer _display = new(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(1) };
    private bool _displayVisible;

    public FocusController(TimeProvider time)
    {
        _store = new JsonFileStore<FocusSnapshot>(App.DataDirectory, "focus.json");
        Timer = new FocusTimer(time, new FocusSettings(), _store.Load());
        _saver = new AutoSaver(() => _store.Save(Timer.Snapshot));

        _completion.Tick += (_, _) =>
        {
            _completion.Stop();
            Timer.Tick();
            Schedule();
        };
        _display.Tick += (_, _) =>
        {
            Timer.Tick();
            Updated?.Invoke();
        };

        Timer.Changed += () =>
        {
            _saver.MarkDirty();
            Schedule();
            Updated?.Invoke();
        };
        Timer.Completed += OnCompleted;

        // Laptop bangun dari sleep: sesi mungkin sudah lewat.
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Timer.Tick();
        Schedule();
    }

    public FocusTimer Timer { get; }

    /// <summary>State atau tampilan (tiap detik saat terlihat) perlu diperbarui.</summary>
    public event Action? Updated;

    public event Action<PeekRequest>? PeekRequested;

    /// <summary>Durasi baru dari Pengaturan; sesi yang sedang berjalan tetap memakai waktu selesainya.</summary>
    public void Apply(int focusMinutes, int shortBreakMinutes, int longBreakMinutes)
    {
        var s = Timer.Settings;
        s.FocusMinutes = focusMinutes;
        s.ShortBreakMinutes = shortBreakMinutes;
        s.LongBreakMinutes = longBreakMinutes;
        Updated?.Invoke();
    }

    public void SetDisplayVisible(bool visible)
    {
        _displayVisible = visible;
        Schedule();
    }

    private void Schedule()
    {
        _completion.Stop();
        if (Timer.EndsAt is { } end)
        {
            var wait = end - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(50);
            _completion.Interval = wait > TimeSpan.Zero ? wait : TimeSpan.FromMilliseconds(50);
            _completion.Start();
        }

        bool tick = _displayVisible && Timer.Status == FocusStatus.Running;
        if (tick && !_display.IsEnabled) _display.Start();
        else if (!tick && _display.IsEnabled) _display.Stop();
    }

    private void OnCompleted(FocusMode finished, FocusMode next)
    {
        var request = finished == FocusMode.Focus
            ? new PeekRequest(PeekKind.FocusDone)
            {
                Detail = next == FocusMode.LongBreak
                    ? $"Istirahat panjang {Timer.Settings.LongBreakMinutes} menit"
                    : $"Istirahat {Timer.Settings.ShortBreakMinutes} menit",
            }
            : new PeekRequest(PeekKind.BreakDone) { Detail = "Siap fokus lagi" };
        PeekRequested?.Invoke(request);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                Timer.Tick();
                Schedule();
                Updated?.Invoke();
            });
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _completion.Stop();
        _display.Stop();
        _saver.Flush();
    }
}
