using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Celah.App.Lyrics;
using Celah.App.Music;
using Celah.App.Peeks;
using Celah.App.Productivity;
using Celah.Core.Notch;
using Celah.Core.Peeks;
using Celah.Platform.Display;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Celah.App.Notch;

/// <summary>Menghubungkan state machine, sumber event platform, dan jendela notch.</summary>
public sealed class NotchController : IDisposable
{
    private static readonly TimeSpan NudgeStep = TimeSpan.FromSeconds(5);

    private readonly NotchWindow _window;
    private readonly NotchStateMachine _stateMachine;
    private readonly MusicController _music;
    private readonly LyricsController _lyrics;
    private readonly SystemPeekController _peeks;
    private readonly FocusController _focus;
    private readonly WaterController _water;
    private readonly ListsController _lists;
    private readonly ILogger<NotchController> _log;
    private readonly DispatcherTimer _dragExpand = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly DispatcherTimer _dragLeave = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private bool _musicLive;
    private ForegroundWindowWatcher? _foreground;

    public NotchController(NotchWindow window, NotchStateMachine stateMachine, MusicController music,
        LyricsController lyrics, SystemPeekController peeks, FocusController focus, WaterController water,
        ListsController lists, ILogger<NotchController> log)
    {
        _window = window;
        _stateMachine = stateMachine;
        _music = music;
        _lyrics = lyrics;
        _peeks = peeks;
        _focus = focus;
        _water = water;
        _lists = lists;
        _log = log;
    }

    public NotchState State => _stateMachine.State;

    public void Start()
    {
        _stateMachine.StateChanged += OnStateChanged;

        _window.HoverTarget.MouseEnter += OnPointerEnter;
        _window.HoverTarget.MouseLeave += OnPointerLeave;

        _music.ActivityChanged += activity =>
        {
            _musicLive = activity == CollapsedActivity.Music;
            UpdateActivity();
        };
        _music.LevelsUpdated += _window.MusicPill.SetLevels;
        var seekBar = _window.MusicModule.SeekBar;
        seekBar.SeekRequested += fraction => _ = _music.SeekAsync(fraction);
        seekBar.Nudged += direction => _ = _music.SeekByAsync(NudgeStep * direction);
        // Panel tetap terbuka selama slider digeser walau kursor keluar dari notch.
        seekBar.DraggingChanged += _stateMachine.SetHoldOpen;

        var lyricsView = _window.LyricsModule;
        _lyrics.LinesChanged += lyricsView.SetLines;
        _lyrics.ActiveLineChanged += lyricsView.SetActive;
        lyricsView.LineClicked += _lyrics.SeekToLine;
        _window.VisibleModuleChanged += module => _lyrics.SetVisible(module == NotchWindow.LyricsTab);

        _peeks.PeekRequested += ShowPeek;

        // ── Fokus, minum, tugas, rak (tahap 5) ──
        _window.FocusModule.Attach(_focus.Timer);
        _window.FocusPill.Update(_focus.Timer);
        _focus.Updated += () =>
        {
            _window.FocusPill.Update(_focus.Timer);
            _window.FocusModule.Refresh();
            UpdateActivity();
        };
        _focus.PeekRequested += ShowPeek;

        _window.WaterModule.Attach(_water);
        _water.Updated += _window.WaterModule.Refresh;
        _water.ReminderChanged += _ => UpdateActivity();
        _window.WaterPill.DrinkClicked += _water.Drink;

        _window.TodoModule.Attach(_lists.Todo);
        _window.TodoModule.TextInputChanged += active =>
        {
            _window.SetTextInput(active);
            _stateMachine.SetHoldOpen(active);
        };

        _window.ShelfModule.Attach(_lists.Shelf);
        _window.ShelfModule.BusyChanged += _stateMachine.SetHoldOpen;
        _window.VisibleModuleChanged += _ => UpdateFocusDisplay();

        var drop = _window.DropTarget;
        drop.DragEnter += OnDragEnter;
        drop.DragOver += OnDragOver;
        // DragLeave juga terpicu saat pindah antar-elemen di dalam notch; tunggu sebentar.
        drop.DragLeave += (_, _) =>
        {
            _dragLeave.Stop();
            _dragLeave.Start();
        };
        drop.Drop += OnDrop;
        _dragExpand.Tick += (_, _) =>
        {
            // File ditahan 600 ms di atas notch: buka penuh ke tab Rak (Shelf.dc.html).
            _dragExpand.Stop();
            _window.SelectModule(NotchWindow.ShelfTab);
            _stateMachine.ExpandNow();
        };
        _dragLeave.Tick += (_, _) => EndDrag(dropped: false);
        UpdateActivity();

        _window.Show();
        _ = _music.StartAsync();
        _ = _peeks.StartAsync();

        _foreground = new ForegroundWindowWatcher(() => NotchWindowInterop.GetMonitorOf(_window.Handle));
        _foreground.FullscreenChanged += OnFullscreenChanged;
        _foreground.CoversTopChanged += OnCoversTopChanged;
        _foreground.ForegroundChanged += _window.EnsureTopmost;
        _foreground.ForegroundChanged += _peeks.OnForegroundChanged;
        _foreground.Start();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _log.LogInformation("Notch aktif");
    }

    public void SetSuspended(bool suspended) => _stateMachine.SetSuspended(suspended);

    public void ShowTestPeek() => _peeks.RaiseSample();

    /// <summary>
    /// Tampilkan event sesaat. Saat panel terbuka, peek menjadi baris di bawah bar tab; saat
    /// fullscreen hanya baterai kritis yang boleh turun.
    /// </summary>
    private void ShowPeek(PeekRequest request)
    {
        _log.LogDebug("Peek {Kind}", request.Kind);
        if (_stateMachine.State == NotchState.Expanded)
        {
            _window.ShowBanner(request);
            return;
        }
        if (_stateMachine.State == NotchState.Hidden && !request.BypassHidden) return;

        _window.SetPeek(request);
        _stateMachine.ShowPeek(request.Hold, request.BypassHidden);
    }

    private void OnStateChanged(NotchState from, NotchState to)
    {
        _log.LogDebug("Notch {From} → {To}", from, to);

        // Hover membuka tab aktivitas terkait (Collapsed.dc.html → Interaksi). Tab Lirik dibiarkan
        // jika terakhir dipilih, supaya pengguna yang membaca lirik tidak dilempar balik ke Musik.
        if (to == NotchState.Expanded && _musicLive
            && _window.SelectedModule is not (NotchWindow.MusicTab or NotchWindow.LyricsTab))
            _window.SelectModule(NotchWindow.MusicTab);

        _window.TransitionTo(from, to);
        _music.SetNotchState(to);
        _window.WaterPill.SetPulsing(to == NotchState.Collapsed && _window.CollapsedActivity == CollapsedActivity.Water);
        if (to != NotchState.Collapsed) _window.FocusPill.StopAnimations();
        UpdateFocusDisplay();
    }

    /// <summary>Isi pil: pengingat minum → timer fokus → musik → idle (ActivityArbiter).</summary>
    private void UpdateActivity()
    {
        _window.SetCollapsedActivity(ActivityArbiter.Pick(_musicLive, _focus.Timer.Status, _water.ReminderActive));
        UpdateFocusDisplay();
    }

    /// <summary>Tick 1 dtk timer fokus hanya saat timernya terlihat (pil atau modul Fokus).</summary>
    private void UpdateFocusDisplay()
    {
        bool pill = _stateMachine.State == NotchState.Collapsed
            && _window.CollapsedActivity is CollapsedActivity.Focus or CollapsedActivity.MusicAndFocus;
        _focus.SetDisplayVisible(pill || _window.VisibleModule == NotchWindow.FocusTab);
    }

    // ── Seret file ke notch → rak ───────────────────────────────────────────

    private static string[]? DraggedFiles(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) ? e.Data.GetData(DataFormats.FileDrop) as string[] : null;

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        _dragLeave.Stop();
        e.Handled = true;
        if (DraggedFiles(e) is not { Length: > 0 } files)
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        e.Effects = DragDropEffects.Copy;
        long bytes = files.Sum(f => File.Exists(f) ? new FileInfo(f).Length : 0);
        _window.ShelfModule.SetDragOver(true, files.Length, bytes);

        if (_stateMachine.State == NotchState.Expanded)
        {
            _window.SelectModule(NotchWindow.ShelfTab);
            return;
        }
        ShowPeek(new PeekRequest(PeekKind.ShelfDrop) { Text = $"{files.Length} file" });
        if (!_dragExpand.IsEnabled) _dragExpand.Start();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        _dragLeave.Stop();
        e.Effects = DraggedFiles(e) is { Length: > 0 } ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var files = DraggedFiles(e);
        int added = files is null ? 0 : _lists.Shelf.Add(files);
        bool expanded = _stateMachine.State == NotchState.Expanded;
        EndDrag(dropped: true);
        // Dilepas langsung tanpa membuka panel: konfirmasi singkat.
        if (!expanded && added > 0)
            ShowPeek(new PeekRequest(PeekKind.ShelfDrop) { Flag = true, Text = $"{added} file" });
    }

    private void EndDrag(bool dropped)
    {
        _dragLeave.Stop();
        _dragExpand.Stop();
        _window.ShelfModule.SetDragOver(false, dropped: dropped);
        _stateMachine.EndPeek();
        // Dibuka lewat seretan (tanpa hover): tutup lagi jika kursor tidak di atas notch.
        if (_stateMachine.State == NotchState.Expanded && !_window.HoverTarget.IsMouseOver)
            _stateMachine.PointerExited();
    }

    private void OnPointerEnter(object sender, MouseEventArgs e)
    {
        // Umpan balik instan sebelum panel mengembang (revisi: hover terasa telat).
        _window.ShowHoverAck(true);
        // Pengingat minum punya chip "+1 gelas" yang bisa diklik: beri waktu sebelum mengembang.
        _stateMachine.PointerEntered(_window.CollapsedActivity == CollapsedActivity.Water
            ? TimeSpan.FromMilliseconds(450)
            : null);
    }

    private void OnPointerLeave(object sender, MouseEventArgs e)
    {
        _window.ShowHoverAck(false);
        _stateMachine.PointerExited();
    }

    private void OnFullscreenChanged(bool fullscreen)
    {
        _log.LogDebug("Fullscreen: {Fullscreen}", fullscreen);
        _stateMachine.SetFullscreen(fullscreen);
    }

    private void OnCoversTopChanged(bool coversTop)
    {
        _log.LogDebug("Jendela menutupi tepi atas: {CoversTop}", coversTop);
        _stateMachine.SetSlim(coversTop);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Event ini datang dari thread lain.
        _window.Dispatcher.BeginInvoke(() =>
        {
            _window.Reposition();
            _foreground?.Evaluate();
        });
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _foreground?.Dispose();
        _peeks.Dispose();
        _focus.Dispose();
        _water.Dispose();
        _lists.Dispose();
        _lyrics.Dispose();
        _music.Dispose();
        _stateMachine.Dispose();
    }
}
