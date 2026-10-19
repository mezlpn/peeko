using System.Windows.Threading;
using Celah.Core.Productivity;
using Celah.Core.Storage;

namespace Celah.App.Productivity;

/// <summary>
/// Pengingat minum. Timer sekali-jalan dipasang ke waktu pengingat berikutnya; saat jatuh tempo
/// pil menampilkan "Waktunya minum" selama 8 dtk (Collapsed.dc.html).
/// </summary>
public sealed class WaterController : IDisposable
{
    public static readonly TimeSpan ReminderDuration = TimeSpan.FromSeconds(8);

    private readonly JsonFileStore<WaterData> _store;
    private readonly AutoSaver _saver;
    private readonly DispatcherTimer _due = new();
    private readonly DispatcherTimer _reminderEnd = new() { Interval = ReminderDuration };

    public WaterController(TimeProvider time)
    {
        _store = new JsonFileStore<WaterData>(App.DataDirectory, "water.json");
        Tracker = new WaterTracker(time, _store.Load());
        _saver = new AutoSaver(() => _store.Save(Tracker.Data));

        _due.Tick += (_, _) =>
        {
            _due.Stop();
            if (Tracker.IsDue) ShowReminder();
            else Schedule();
        };
        _reminderEnd.Tick += (_, _) => EndReminder();

        Tracker.Changed += () =>
        {
            _saver.MarkDirty();
            Schedule();
            Updated?.Invoke();
        };
        Schedule();
    }

    public WaterTracker Tracker { get; }

    /// <summary>Pengingat sedang tampil di pil.</summary>
    public bool ReminderActive { get; private set; }

    public event Action? Updated;

    /// <summary>Pengingat mulai/selesai tampil.</summary>
    public event Action<bool>? ReminderChanged;

    public void Drink()
    {
        Tracker.Drink();
        EndReminder();
    }

    public void Undo() => Tracker.Undo();

    /// <summary>Terapkan pengaturan. Modul mati: tidak ada pengingat sama sekali.</summary>
    public void Apply(bool enabled, int goal, int intervalMinutes)
    {
        _enabled = enabled;
        Tracker.Data.Goal = goal;
        Tracker.Data.IntervalMinutes = intervalMinutes;
        if (!enabled) EndReminder();
        _saver.MarkDirty();
        Schedule();
        Updated?.Invoke();
    }

    private bool _enabled = true;

    private void Schedule()
    {
        _due.Stop();
        if (!_enabled || Tracker.NextReminder is not { } next) return;
        var wait = next - DateTimeOffset.UtcNow;
        // Jaga-jaga lewat tengah malam: hitung ulang paling lambat tiap jam.
        if (wait > TimeSpan.FromHours(1)) wait = TimeSpan.FromHours(1);
        _due.Interval = wait > TimeSpan.Zero ? wait : TimeSpan.FromSeconds(1);
        _due.Start();
    }

    private void ShowReminder()
    {
        Tracker.MarkReminded();
        ReminderActive = true;
        ReminderChanged?.Invoke(true);
        _reminderEnd.Stop();
        _reminderEnd.Start();
    }

    private void EndReminder()
    {
        _reminderEnd.Stop();
        if (!ReminderActive) return;
        ReminderActive = false;
        ReminderChanged?.Invoke(false);
    }

    public void Dispose()
    {
        _due.Stop();
        _reminderEnd.Stop();
        _saver.Flush();
    }
}
