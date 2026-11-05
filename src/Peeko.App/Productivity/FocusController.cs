using System.Windows.Threading;
using Peeko.Core.Peeks;
using Peeko.Core.Productivity;
using Peeko.Core.Storage;
using Microsoft.Win32;

namespace Peeko.App.Productivity;

/// <summary>
/// Timer fokus (Pomodoro), timer bebas, dan stopwatch. Satu timer sekali-jalan tepat di akhir sesi;
/// tick 1 dtk untuk tampilan hanya berjalan saat timernya terlihat (pil atau modul Fokus).
/// </summary>
public sealed class FocusController : IDisposable
{
    private readonly JsonFileStore<FocusSnapshot> _store;
    private readonly AutoSaver _saver;
    private readonly JsonFileStore<TimersSnapshot> _timersStore;
    private readonly AutoSaver _timersSaver;
    private readonly TimersSnapshot _timers;
    private readonly DispatcherTimer _completion = new(DispatcherPriority.Normal);
    private readonly DispatcherTimer _display = new(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(1) };
    private bool _displayVisible;

    public FocusController(TimeProvider time)
    {
        _store = new JsonFileStore<FocusSnapshot>(App.DataDirectory, "focus.json");
        Timer = new FocusTimer(time, new FocusSettings(), _store.Load());
        _saver = new AutoSaver(() => _store.Save(Timer.Snapshot));

        _timersStore = new JsonFileStore<TimersSnapshot>(App.DataDirectory, "timers.json");
        _timers = _timersStore.Load() ?? new TimersSnapshot();
        _timersSaver = new AutoSaver(() => _timersStore.Save(_timers));
        Countdown = new CountdownTimer(time, _timers);
        Stopwatch = new StopwatchTimer(time, _timers);
        foreach (var changed in new Action<Action>[] { a => Countdown.Changed += a, a => Stopwatch.Changed += a })
            changed(() =>
            {
                _timersSaver.MarkDirty();
                Schedule();
                Updated?.Invoke();
            });
        Countdown.Completed += () => PeekRequested?.Invoke(new PeekRequest(PeekKind.TimerDone)
        {
            Detail = $"{Format(Countdown.Duration)} selesai",
        });

        _completion.Tick += (_, _) =>
        {
            _completion.Stop();
            Timer.Tick();
            Countdown.Tick();
            Schedule();
        };
        _display.Tick += (_, _) =>
        {
            Timer.Tick();
            Countdown.Tick();
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

    public CountdownTimer Countdown { get; }

    public StopwatchTimer Stopwatch { get; }

    /// <summary>Tampilan yang dipilih di modul (Pomodoro / Timer / Stopwatch).</summary>
    public TimerKind Selected
    {
        get => _timers.Selected;
        set
        {
            if (_timers.Selected == value) return;
            _timers.Selected = value;
            _timersSaver.MarkDirty();
            Updated?.Invoke();
        }
    }

    /// <summary>
    /// Timer yang tampil di pil saat tertutup: yang berjalan diutamakan (Pomodoro, timer, lalu
    /// stopwatch), lalu yang dijeda. Null jika semuanya diam.
    /// </summary>
    public TimerKind? PillKind
    {
        get
        {
            foreach (var status in new[] { FocusStatus.Running, FocusStatus.Paused })
            {
                if (Timer.Status == status) return TimerKind.Pomodoro;
                if (Countdown.Status == status) return TimerKind.Countdown;
                if (Stopwatch.Status == status) return TimerKind.Stopwatch;
            }
            return null;
        }
    }

    /// <summary>Status gabungan untuk isi pil (ActivityArbiter).</summary>
    public FocusStatus CombinedStatus => PillKind switch
    {
        TimerKind.Pomodoro => Timer.Status,
        TimerKind.Countdown => Countdown.Status,
        TimerKind.Stopwatch => Stopwatch.Status,
        _ => FocusStatus.Ready,
    };

    /// <summary>"05:00", atau "1:05:00" bila lebih dari satu jam.</summary>
    public static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";

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
        DateTimeOffset? next = (Timer.EndsAt, Countdown.EndsAt) switch
        {
            ({ } a, { } b) => a < b ? a : b,
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => null,
        };
        if (next is { } end)
        {
            var wait = end - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(50);
            _completion.Interval = wait > TimeSpan.Zero ? wait : TimeSpan.FromMilliseconds(50);
            _completion.Start();
        }

        bool tick = _displayVisible && (Timer.Status == FocusStatus.Running || Countdown.Status == FocusStatus.Running
            || Stopwatch.Status == FocusStatus.Running);
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
                Countdown.Tick();
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
        _timersSaver.Flush();
    }
}
