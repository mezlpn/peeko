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
    private FullscreenWatcher? _fullscreen;

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

        _fullscreen = new FullscreenWatcher(() => NotchWindowInterop.GetMonitorOf(_window.Handle));
        _fullscreen.FullscreenChanged += OnFullscreenChanged;
        _fullscreen.ForegroundChanged += _window.EnsureTopmost;
        _fullscreen.Start();

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

    private void OnPointerEnter(object sender, MouseEventArgs e) => _stateMachine.PointerEntered();

    private void OnPointerLeave(object sender, MouseEventArgs e) => _stateMachine.PointerExited();

    private void OnFullscreenChanged(bool fullscreen)
    {
        _log.LogDebug("Fullscreen: {Fullscreen}", fullscreen);
        _stateMachine.SetFullscreen(fullscreen);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Event ini datang dari thread lain.
        _window.Dispatcher.BeginInvoke(() =>
        {
            _window.Reposition();
            _fullscreen?.Evaluate();
        });
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _fullscreen?.Dispose();
        _stateMachine.Dispose();
    }
}
