namespace Celah.Core.Notch;

/// <summary>
/// Logika state notch tanpa ketergantungan UI. Semua method harus dipanggil dari satu thread
/// (thread UI). Callback timer dikembalikan ke thread itu lewat <c>post</c>.
/// </summary>
public sealed class NotchStateMachine : IDisposable
{
    private readonly TimeProvider _time;
    private readonly Action<Action> _post;
    private readonly NotchTimings _timings;

    private readonly DelayedAction _hoverOpen;
    private readonly DelayedAction _leaveClose;
    private readonly DelayedAction _peekEnd;
    private readonly DelayedAction _fullscreenSettle;

    private bool _pointerOver;
    private bool _holdOpen;
    private bool _fullscreen;
    private bool _suspended;

    public NotchStateMachine(TimeProvider time, Action<Action> post, NotchTimings? timings = null)
    {
        _time = time;
        _post = post;
        _timings = timings ?? NotchTimings.Default;

        _hoverOpen = new DelayedAction(this, OnHoverOpenElapsed);
        _leaveClose = new DelayedAction(this, OnLeaveCloseElapsed);
        _peekEnd = new DelayedAction(this, OnPeekEndElapsed);
        _fullscreenSettle = new DelayedAction(this, OnFullscreenSettled);
    }

    public NotchState State { get; private set; } = NotchState.Collapsed;

    public bool IsPointerOver => _pointerOver;

    /// <summary>(state lama, state baru)</summary>
    public event Action<NotchState, NotchState>? StateChanged;

    private bool ShouldHide => _fullscreen || _suspended;

    /// <param name="openDelay">
    /// Jeda sebelum mengembang. Area yang bisa diklik di mode collapsed memakai jeda lebih panjang
    /// supaya klik sempat terjadi (keputusan desain #4).
    /// </param>
    public void PointerEntered(TimeSpan? openDelay = null)
    {
        _pointerOver = true;
        _leaveClose.Cancel();

        if (State is NotchState.Collapsed or NotchState.Peek)
            _hoverOpen.Start(openDelay ?? _timings.HoverOpenDelay);
    }

    /// <summary>Kursor pindah ke area dengan jeda berbeda tanpa keluar dari notch.</summary>
    public void UpdateOpenDelay(TimeSpan openDelay)
    {
        if (_pointerOver && _hoverOpen.IsPending)
            _hoverOpen.Start(openDelay);
    }

    public void PointerExited()
    {
        _pointerOver = false;
        _hoverOpen.Cancel();

        if (State == NotchState.Expanded && !_holdOpen)
            _leaveClose.Start(_timings.LeaveCloseDelay);
        else if (State == NotchState.Peek && !_peekEnd.IsPending)
            _peekEnd.Start(_timings.PeekHold);
    }

    /// <summary>Tahan panel tetap terbuka walau kursor keluar (mis. saat input teks fokus).</summary>
    public void SetHoldOpen(bool hold)
    {
        _holdOpen = hold;
        if (hold)
            _leaveClose.Cancel();
        else if (State == NotchState.Expanded && !_pointerOver)
            _leaveClose.Start(_timings.LeaveCloseDelay);
    }

    public void SetFullscreen(bool fullscreen)
    {
        if (fullscreen)
        {
            _fullscreenSettle.Cancel();
            if (_fullscreen) return;
            _fullscreen = true;
            Hide();
        }
        else if (_fullscreen && !_fullscreenSettle.IsPending)
        {
            // Tunggu sebentar supaya alt-tab cepat tidak membuat notch naik-turun.
            _fullscreenSettle.Start(_timings.FullscreenSettle);
        }
    }

    /// <summary>"Sembunyikan sementara" dari menu tray.</summary>
    public void SetSuspended(bool suspended)
    {
        if (_suspended == suspended) return;
        _suspended = suspended;
        if (suspended)
            Hide();
        else if (!ShouldHide)
            SetState(NotchState.Collapsed);
    }

    /// <summary>Tampilkan peek. Mengembalikan false jika peek tidak bisa tampil sebagai pil.</summary>
    /// <param name="bypassHidden">Untuk event penting (baterai kritis) yang tetap tampil saat fullscreen.</param>
    public bool ShowPeek(TimeSpan? hold = null, bool bypassHidden = false)
    {
        if (State == NotchState.Expanded) return false;
        if (State == NotchState.Hidden && !bypassHidden) return false;
        if (_suspended) return false;

        SetState(NotchState.Peek);
        _peekEnd.Start(hold ?? _timings.PeekHold);
        return true;
    }

    private void Hide()
    {
        _hoverOpen.Cancel();
        _leaveClose.Cancel();
        _peekEnd.Cancel();
        SetState(NotchState.Hidden);
    }

    private void OnHoverOpenElapsed()
    {
        if (_pointerOver && State is NotchState.Collapsed or NotchState.Peek)
        {
            _peekEnd.Cancel();
            SetState(NotchState.Expanded);
        }
    }

    private void OnLeaveCloseElapsed()
    {
        if (State == NotchState.Expanded && !_pointerOver && !_holdOpen)
            SetState(NotchState.Collapsed);
    }

    private void OnPeekEndElapsed()
    {
        if (State != NotchState.Peek) return;
        if (_pointerOver) return; // hover menahan peek; PointerExited memulai ulang hitungan
        SetState(ShouldHide ? NotchState.Hidden : NotchState.Collapsed);
    }

    private void OnFullscreenSettled()
    {
        _fullscreen = false;
        if (State == NotchState.Hidden && !ShouldHide)
            SetState(NotchState.Collapsed);
    }

    private void SetState(NotchState next)
    {
        if (State == next) return;
        var previous = State;
        State = next;
        StateChanged?.Invoke(previous, next);
    }

    public void Dispose()
    {
        _hoverOpen.Dispose();
        _leaveClose.Dispose();
        _peekEnd.Dispose();
        _fullscreenSettle.Dispose();
    }

    /// <summary>Timer sekali jalan yang bisa dibatalkan; callback basi diabaikan lewat nomor generasi.</summary>
    private sealed class DelayedAction(NotchStateMachine owner, Action action) : IDisposable
    {
        private ITimer? _timer;
        private int _generation;

        public bool IsPending { get; private set; }

        public void Start(TimeSpan delay)
        {
            Cancel();
            IsPending = true;
            var generation = _generation;
            _timer = owner._time.CreateTimer(
                _ => owner._post(() =>
                {
                    if (generation != _generation || !IsPending) return;
                    IsPending = false;
                    action();
                }),
                null, delay, Timeout.InfiniteTimeSpan);
        }

        public void Cancel()
        {
            _generation++;
            IsPending = false;
            _timer?.Dispose();
            _timer = null;
        }

        public void Dispose() => Cancel();
    }
}
