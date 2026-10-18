namespace Celah.Core.Productivity;

public enum FocusMode
{
    Focus,
    ShortBreak,
    LongBreak,
}

public enum FocusStatus
{
    /// <summary>Siap dimulai (belum berjalan).</summary>
    Ready,
    Running,
    Paused,
}

public sealed class FocusSettings
{
    public int FocusMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int SessionsPerCycle { get; set; } = 4;
}

/// <summary>State yang disimpan ke disk supaya timer bertahan setelah aplikasi ditutup/sleep.</summary>
public sealed class FocusSnapshot
{
    public FocusMode Mode { get; set; }
    public FocusStatus Status { get; set; }
    public int Session { get; set; } = 1;
    public DateTimeOffset? EndsAt { get; set; }
    public TimeSpan? Remaining { get; set; }
    public DateTimeOffset? PausedAt { get; set; }
}

/// <summary>
/// Timer Pomodoro. Menyimpan waktu selesai (<c>EndsAt</c>), bukan sisa detik, jadi tetap akurat
/// setelah laptop sleep. Alur: fokus selesai → istirahat langsung berjalan → istirahat selesai →
/// fokus berikutnya siap (menunggu ditekan). Istirahat panjang setelah sesi ke-4.
/// </summary>
public sealed class FocusTimer
{
    private readonly TimeProvider _time;
    private FocusSnapshot _s;

    public FocusTimer(TimeProvider time, FocusSettings settings, FocusSnapshot? restored = null)
    {
        _time = time;
        Settings = settings;
        _s = restored ?? new FocusSnapshot();
        if (_s.Status == FocusStatus.Ready) _s.Remaining = null;
    }

    public FocusSettings Settings { get; }

    public FocusMode Mode => _s.Mode;
    public FocusStatus Status => _s.Status;
    public int Session => _s.Session;
    public DateTimeOffset? EndsAt => _s.Status == FocusStatus.Running ? _s.EndsAt : null;
    public DateTimeOffset? PausedAt => _s.Status == FocusStatus.Paused ? _s.PausedAt : null;

    /// <summary>Sesi yang sudah selesai dalam siklus ini (untuk titik sesi).</summary>
    public int CompletedSessions => _s.Mode == FocusMode.Focus ? _s.Session - 1 : _s.Session;

    /// <summary>Mode selesai: (mode yang selesai, mode berikutnya).</summary>
    public event Action<FocusMode, FocusMode>? Completed;

    /// <summary>State berubah (untuk disimpan dan ditampilkan ulang).</summary>
    public event Action? Changed;

    public FocusSnapshot Snapshot => new()
    {
        Mode = _s.Mode,
        Status = _s.Status,
        Session = _s.Session,
        EndsAt = _s.EndsAt,
        Remaining = _s.Remaining,
        PausedAt = _s.PausedAt,
    };

    public TimeSpan Duration(FocusMode mode) => TimeSpan.FromMinutes(mode switch
    {
        FocusMode.Focus => Settings.FocusMinutes,
        FocusMode.ShortBreak => Settings.ShortBreakMinutes,
        _ => Settings.LongBreakMinutes,
    });

    public TimeSpan Remaining
    {
        get
        {
            var r = _s.Status switch
            {
                FocusStatus.Running => (_s.EndsAt ?? _time.GetUtcNow()) - _time.GetUtcNow(),
                FocusStatus.Paused => _s.Remaining ?? Duration(_s.Mode),
                _ => Duration(_s.Mode),
            };
            return r < TimeSpan.Zero ? TimeSpan.Zero : r;
        }
    }

    /// <summary>Sisa dalam pecahan 0–1 (untuk garis progress yang menyusut).</summary>
    public double RemainingFraction => Remaining / Duration(_s.Mode);

    public void Start()
    {
        if (_s.Status == FocusStatus.Running) return;
        var remaining = _s.Status == FocusStatus.Paused ? Remaining : Duration(_s.Mode);
        _s.EndsAt = _time.GetUtcNow() + remaining;
        _s.Remaining = null;
        _s.PausedAt = null;
        _s.Status = FocusStatus.Running;
        Changed?.Invoke();
    }

    public void Pause()
    {
        if (_s.Status != FocusStatus.Running) return;
        _s.Remaining = Remaining;
        _s.PausedAt = _time.GetUtcNow();
        _s.EndsAt = null;
        _s.Status = FocusStatus.Paused;
        Changed?.Invoke();
    }

    /// <summary>Kembali ke awal mode sekarang (tidak berjalan).</summary>
    public void Reset()
    {
        _s.Status = FocusStatus.Ready;
        _s.EndsAt = null;
        _s.Remaining = null;
        _s.PausedAt = null;
        Changed?.Invoke();
    }

    /// <summary>Lewati ke mode berikutnya tanpa menunggu.</summary>
    public void Skip() => Advance(notify: false);

    /// <summary>Dipanggil berkala/oleh timer: menyelesaikan mode jika waktunya habis.</summary>
    public void Tick()
    {
        if (_s.Status == FocusStatus.Running && Remaining <= TimeSpan.Zero)
            Advance(notify: true);
    }

    private void Advance(bool notify)
    {
        var finished = _s.Mode;
        FocusMode next;
        if (finished == FocusMode.Focus)
        {
            next = _s.Session >= Settings.SessionsPerCycle ? FocusMode.LongBreak : FocusMode.ShortBreak;
        }
        else
        {
            next = FocusMode.Focus;
            _s.Session = finished == FocusMode.LongBreak ? 1 : _s.Session + 1;
        }

        _s.Mode = next;
        _s.Remaining = null;
        _s.PausedAt = null;

        // Istirahat langsung berjalan; fokus berikutnya menunggu pengguna.
        if (next != FocusMode.Focus)
        {
            _s.Status = FocusStatus.Running;
            _s.EndsAt = _time.GetUtcNow() + Duration(next);
        }
        else
        {
            _s.Status = FocusStatus.Ready;
            _s.EndsAt = null;
        }

        if (notify) Completed?.Invoke(finished, next);
        Changed?.Invoke();
    }
}
