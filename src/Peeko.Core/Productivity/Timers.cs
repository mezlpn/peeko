namespace Peeko.Core.Productivity;

/// <summary>Tampilan yang dipilih di modul fokus.</summary>
public enum TimerKind { Pomodoro, Countdown, Stopwatch }

/// <summary>State timer bebas dan stopwatch yang disimpan ke disk (timers.json).</summary>
public sealed class TimersSnapshot
{
    public TimerKind Selected { get; set; }
    public TimeSpan CountdownDuration { get; set; } = TimeSpan.FromMinutes(5);
    public DateTimeOffset? CountdownEndsAt { get; set; }
    public TimeSpan? CountdownRemaining { get; set; }
    public DateTimeOffset? StopwatchStartedAt { get; set; }
    public TimeSpan StopwatchAccumulated { get; set; }
    public List<TimeSpan> Laps { get; set; } = [];
}

/// <summary>
/// Timer dengan durasi bebas. Seperti Pomodoro, yang disimpan waktu selesainya, jadi tetap akurat
/// setelah laptop sleep atau Peeko ditutup.
/// </summary>
public sealed class CountdownTimer
{
    public static readonly TimeSpan Max = TimeSpan.FromHours(10);

    private readonly TimeProvider _time;
    private readonly TimersSnapshot _s;

    public CountdownTimer(TimeProvider time, TimersSnapshot s)
    {
        _time = time;
        _s = s;
    }

    public event Action? Changed;

    /// <summary>Waktu habis (dipanggil oleh <see cref="Tick"/>).</summary>
    public event Action? Completed;

    public TimeSpan Duration => _s.CountdownDuration;
    public DateTimeOffset? EndsAt => _s.CountdownEndsAt;

    public FocusStatus Status => _s.CountdownEndsAt is not null ? FocusStatus.Running
        : _s.CountdownRemaining is not null ? FocusStatus.Paused : FocusStatus.Ready;

    public TimeSpan Remaining
    {
        get
        {
            var r = Status switch
            {
                FocusStatus.Running => _s.CountdownEndsAt!.Value - _time.GetUtcNow(),
                FocusStatus.Paused => _s.CountdownRemaining!.Value,
                _ => _s.CountdownDuration,
            };
            return r < TimeSpan.Zero ? TimeSpan.Zero : r;
        }
    }

    public double RemainingFraction => _s.CountdownDuration <= TimeSpan.Zero ? 0 : Math.Clamp(Remaining / _s.CountdownDuration, 0, 1);

    /// <summary>Durasi untuk timer berikutnya (hanya saat belum berjalan).</summary>
    public void SetDuration(TimeSpan duration)
    {
        if (Status != FocusStatus.Ready) return;
        _s.CountdownDuration = Clamp(duration);
        Changed?.Invoke();
    }

    public void Start()
    {
        if (Status == FocusStatus.Running) return;
        _s.CountdownEndsAt = _time.GetUtcNow() + (Status == FocusStatus.Paused ? Remaining : _s.CountdownDuration);
        _s.CountdownRemaining = null;
        Changed?.Invoke();
    }

    public void Pause()
    {
        if (Status != FocusStatus.Running) return;
        _s.CountdownRemaining = Remaining;
        _s.CountdownEndsAt = null;
        Changed?.Invoke();
    }

    public void Reset()
    {
        _s.CountdownEndsAt = null;
        _s.CountdownRemaining = null;
        Changed?.Invoke();
    }

    /// <summary>Tambah waktu saat berjalan/dijeda ("+1 mnt"); saat siap, ubah durasinya.</summary>
    public void Add(TimeSpan amount)
    {
        switch (Status)
        {
            case FocusStatus.Running:
                _s.CountdownEndsAt += amount;
                _s.CountdownDuration = Clamp(_s.CountdownDuration + amount);
                break;
            case FocusStatus.Paused:
                _s.CountdownRemaining = Clamp(_s.CountdownRemaining!.Value + amount);
                _s.CountdownDuration = Clamp(_s.CountdownDuration + amount);
                break;
            default:
                _s.CountdownDuration = Clamp(_s.CountdownDuration + amount);
                break;
        }
        Changed?.Invoke();
    }

    public void Tick()
    {
        if (Status == FocusStatus.Running && Remaining <= TimeSpan.Zero)
        {
            _s.CountdownEndsAt = null;
            _s.CountdownRemaining = null;
            Completed?.Invoke();
            Changed?.Invoke();
        }
    }

    private static TimeSpan Clamp(TimeSpan t) => t < TimeSpan.FromSeconds(10) ? TimeSpan.FromSeconds(10) : t > Max ? Max : t;
}

/// <summary>Stopwatch: waktu mulai + akumulasi sebelum dijeda, plus putaran (lap).</summary>
public sealed class StopwatchTimer
{
    public const int MaxLaps = 99;

    private readonly TimeProvider _time;
    private readonly TimersSnapshot _s;

    public StopwatchTimer(TimeProvider time, TimersSnapshot s)
    {
        _time = time;
        _s = s;
    }

    public event Action? Changed;

    public FocusStatus Status => _s.StopwatchStartedAt is not null ? FocusStatus.Running
        : _s.StopwatchAccumulated > TimeSpan.Zero ? FocusStatus.Paused : FocusStatus.Ready;

    public TimeSpan Elapsed => _s.StopwatchAccumulated + (_s.StopwatchStartedAt is { } at ? _time.GetUtcNow() - at : TimeSpan.Zero);

    /// <summary>Waktu tiap putaran (bukan total), terbaru di akhir.</summary>
    public IReadOnlyList<TimeSpan> Laps => _s.Laps;

    public void Start()
    {
        if (Status == FocusStatus.Running) return;
        _s.StopwatchStartedAt = _time.GetUtcNow();
        Changed?.Invoke();
    }

    public void Pause()
    {
        if (_s.StopwatchStartedAt is not { } at) return;
        _s.StopwatchAccumulated += _time.GetUtcNow() - at;
        _s.StopwatchStartedAt = null;
        Changed?.Invoke();
    }

    public void Reset()
    {
        _s.StopwatchStartedAt = null;
        _s.StopwatchAccumulated = TimeSpan.Zero;
        _s.Laps.Clear();
        Changed?.Invoke();
    }

    public void Lap()
    {
        if (Status == FocusStatus.Ready || _s.Laps.Count >= MaxLaps) return;
        var total = Elapsed;
        var previous = _s.Laps.Aggregate(TimeSpan.Zero, (a, b) => a + b);
        _s.Laps.Add(total - previous);
        Changed?.Invoke();
    }
}
