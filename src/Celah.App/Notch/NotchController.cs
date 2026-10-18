using System.Windows.Input;
using Celah.Core.Notch;
using Celah.Platform.Display;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Celah.App.Notch;

/// <summary>Menghubungkan state machine, sumber event platform, dan jendela notch.</summary>
public sealed class NotchController : IDisposable
{
    private readonly NotchWindow _window;
    private readonly NotchStateMachine _stateMachine;
    private readonly ILogger<NotchController> _log;
    private ForegroundWindowWatcher? _foreground;

    public NotchController(NotchWindow window, NotchStateMachine stateMachine, ILogger<NotchController> log)
    {
        _window = window;
        _stateMachine = stateMachine;
        _log = log;
    }

    public NotchState State => _stateMachine.State;

    public void Start()
    {
        _stateMachine.StateChanged += OnStateChanged;

        _window.HoverTarget.MouseEnter += OnPointerEnter;
        _window.HoverTarget.MouseLeave += OnPointerLeave;

        _window.Show();

        _foreground = new ForegroundWindowWatcher(() => NotchWindowInterop.GetMonitorOf(_window.Handle));
        _foreground.FullscreenChanged += OnFullscreenChanged;
        _foreground.CoversTopChanged += OnCoversTopChanged;
        _foreground.ForegroundChanged += _window.EnsureTopmost;
        _foreground.Start();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _log.LogInformation("Notch aktif");
    }

    public void SetSuspended(bool suspended) => _stateMachine.SetSuspended(suspended);

    public void ShowTestPeek() => _stateMachine.ShowPeek();

    private void OnStateChanged(NotchState from, NotchState to)
    {
        _log.LogDebug("Notch {From} → {To}", from, to);
        _window.TransitionTo(from, to);
    }

    private void OnPointerEnter(object sender, MouseEventArgs e)
    {
        // Umpan balik instan sebelum panel mengembang (revisi: hover terasa telat).
        _window.ShowHoverAck(true);
        _stateMachine.PointerEntered();
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
        _stateMachine.Dispose();
    }
}
