using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Peeko.App.Animation;
using Peeko.App.Settings;

namespace Peeko.App.Productivity;

public partial class HealthModule : UserControl
{
    // Napas kotak: tarik 4 · tahan 4 · hembus 4 · tahan 4, empat putaran ≈ 1 menit.
    private static readonly (string Label, double Scale)[] Phases = [("Tarik", 1), ("Tahan", 1), ("Hembus", 0.45), ("Tahan", 0.45)];
    private const int PhaseSeconds = 4, Rounds = 4;

    private readonly DispatcherTimer _breathTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private HealthController? _health;
    private SettingsService? _settings;
    private int _second;

    public HealthModule()
    {
        InitializeComponent();
        _breathTimer.Tick += (_, _) => BreathStep();
    }

    /// <summary>Latihan napas berjalan: panel ditahan terbuka.</summary>
    public event Action<bool>? BreathingChanged;

    public void Attach(HealthController health, SettingsService settings)
    {
        _health = health;
        _settings = settings;
        Refresh();
    }

    public void Refresh()
    {
        if (_health is null || _settings is null) return;
        var s = _settings.Current;
        BreakToggle.IsChecked = s.BreaksEnabled;
        EyeToggle.IsChecked = s.EyeBreaksEnabled;
        BreakInfo.Text = s.BreaksEnabled ? $"Tiap {s.BreakIntervalMinutes} mnt · {_health.Breaks}× hari ini" : "Mati";
        EyeInfo.Text = s.EyeBreaksEnabled ? $"Tiap 20 mnt · {_health.EyeBreaks}× hari ini" : "Mati";
        BreathInfo.Text = _health.Breaths == 0 ? "Napas kotak 4-4-4-4" : $"{_health.Breaths}× hari ini";
    }

    private void OnBreakToggle(object sender, RoutedEventArgs e) =>
        _settings?.Update(s => s.BreaksEnabled = BreakToggle.IsChecked == true);

    private void OnEyeToggle(object sender, RoutedEventArgs e) =>
        _settings?.Update(s => s.EyeBreaksEnabled = EyeToggle.IsChecked == true);

    // ── Latihan napas ──────────────────────────────────────────────────────

    private void OnBreathe(object sender, RoutedEventArgs e)
    {
        _second = 0;
        Cards.Visibility = Visibility.Collapsed;
        BreathView.Visibility = Visibility.Visible;
        BreathingChanged?.Invoke(true);
        ApplyPhase(0);
        _breathTimer.Start();
    }

    private void BreathStep()
    {
        _second++;
        if (_second >= Phases.Length * PhaseSeconds * Rounds)
        {
            _health?.BreathDone();
            StopBreath();
            return;
        }
        if (_second % PhaseSeconds == 0) ApplyPhase(_second / PhaseSeconds % Phases.Length);
        BreathCount.Text = $"{PhaseSeconds - _second % PhaseSeconds} · putaran {_second / (PhaseSeconds * Phases.Length) + 1}/{Rounds}";
    }

    private void ApplyPhase(int phase)
    {
        var (label, scale) = Phases[phase];
        BreathPhase.Text = label;
        BreathCount.Text = $"{PhaseSeconds} · putaran {_second / (PhaseSeconds * Phases.Length) + 1}/{Rounds}";
        var duration = Motion.Reduced ? TimeSpan.Zero : TimeSpan.FromSeconds(PhaseSeconds);
        var a = new DoubleAnimation(scale, duration) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        Timeline.SetDesiredFrameRate(a, 30);
        BreathScale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        BreathScale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    private void OnStopBreath(object sender, RoutedEventArgs e) => StopBreath();

    /// <summary>Berhenti (juga dipanggil saat panel ditutup atau pindah tampilan).</summary>
    public void StopBreath()
    {
        if (!_breathTimer.IsEnabled && BreathView.Visibility != Visibility.Visible) return;
        _breathTimer.Stop();
        BreathScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        BreathScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        BreathView.Visibility = Visibility.Collapsed;
        Cards.Visibility = Visibility.Visible;
        BreathingChanged?.Invoke(false);
        Refresh();
    }
}
