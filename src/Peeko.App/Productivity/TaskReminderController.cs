using System.Windows.Threading;
using Microsoft.Win32;
using Peeko.App.Settings;
using Peeko.Core.Peeks;
using Peeko.Core.Productivity;

namespace Peeko.App.Productivity;

/// <summary>
/// Pengingat tugas bertenggat. Beberapa menit sebelum tenggat, pil menampilkan tugasnya dengan hitung
/// mundur; saat tenggat tiba, muncul peek dan pil memberi tombol centang dan tunda selama 10 menit.
/// Satu timer sekali-jalan ke kejadian berikutnya, tanpa polling.
/// </summary>
public sealed class TaskReminderController : IDisposable
{
    /// <summary>Lama pil "sekarang" tetap tampil setelah tenggat.</summary>
    public static readonly TimeSpan DueWindow = TimeSpan.FromMinutes(10);

    private readonly TodoList _todo;
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _next = new(DispatcherPriority.Normal);
    private readonly DispatcherTimer _minute = new(DispatcherPriority.Background);
    private (Guid, DateTime)? _announced;
    private bool _displayVisible;

    public TaskReminderController(ListsController lists, SettingsService settings)
    {
        _todo = lists.Todo;
        _settings = settings;
        _next.Tick += (_, _) =>
        {
            _next.Stop();
            Evaluate();
        };
        _minute.Tick += (_, _) =>
        {
            _minute.Interval = UntilNextMinute();
            Updated?.Invoke();
        };
        _todo.Changed += Evaluate;
        SystemEvents.PowerModeChanged += OnPower;
    }

    public event Action? Updated;

    public event Action<PeekRequest>? PeekRequested;

    /// <summary>Tugas yang sedang tampil di pil (hitung mundur atau sudah waktunya), atau null.</summary>
    public TodoItem? Active { get; private set; }

    public bool ActiveIsDue => Active?.Due is { } due && due <= DateTime.Now;

    public TodoList Todo => _todo;

    public void SetDisplayVisible(bool visible)
    {
        _displayVisible = visible;
        UpdateTick();
    }

    public void Evaluate()
    {
        _next.Stop();
        var s = _settings.Current;
        Active = null;
        if (!s.TodoEnabled || !s.TaskRemindersEnabled)
        {
            UpdateTick();
            Updated?.Invoke();
            return;
        }

        var now = DateTime.Now;
        var countdown = TimeSpan.FromMinutes(s.TaskCountdownMinutes);
        var next = _todo.Next(DueWindow);
        if (next?.Due is { } due)
        {
            if (now >= due)
            {
                Active = next;
                if (_announced != (next.Id, due))
                {
                    _announced = (next.Id, due);
                    PeekRequested?.Invoke(new PeekRequest(PeekKind.TaskDue) { Text = next.Text, Detail = due.ToString("HH.mm") });
                }
                Schedule(due + DueWindow, now);
            }
            else if (s.TaskCountdownMinutes > 0 && now >= due - countdown)
            {
                Active = next;
                Schedule(due, now);
            }
            else
            {
                Schedule(s.TaskCountdownMinutes > 0 ? due - countdown : due, now);
            }
        }
        UpdateTick();
        Updated?.Invoke();
    }

    private void Schedule(DateTime at, DateTime now)
    {
        var wait = at - now + TimeSpan.FromMilliseconds(200);
        _next.Interval = wait > TimeSpan.FromMilliseconds(200) ? wait : TimeSpan.FromMilliseconds(200);
        _next.Start();
    }

    public void Complete()
    {
        if (Active is { } item) _todo.Toggle(item.Id);
    }

    public void Snooze()
    {
        if (Active is { } item) _todo.Snooze(item.Id, TimeSpan.FromMinutes(10));
    }

    private void UpdateTick()
    {
        bool tick = _displayVisible && Active is not null;
        if (tick && !_minute.IsEnabled)
        {
            _minute.Interval = UntilNextMinute();
            _minute.Start();
        }
        else if (!tick) _minute.Stop();
    }

    private static TimeSpan UntilNextMinute()
    {
        var now = DateTime.Now;
        return TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond) + TimeSpan.FromMilliseconds(50);
    }

    private void OnPower(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) System.Windows.Application.Current.Dispatcher.BeginInvoke(Evaluate);
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPower;
        _next.Stop();
        _minute.Stop();
    }
}
