using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Peeko.Core.Productivity;

namespace Peeko.App.Productivity;

public partial class FocusModule : UserControl
{
    private const double W = 224, H = 100, Stroke = 4;
    private static readonly Geometry Stadium = BuildStadium();
    private static readonly double Perimeter = 2 * (W - H) + Math.PI * (H - Stroke);
    private static readonly int[] PresetMinutes = [5, 15, 30];

    private FocusController? _focus;
    private bool _syncing;

    public FocusModule()
    {
        InitializeComponent();
        Track.Data = Stadium;
        Progress.Data = Stadium;
        foreach (int m in PresetMinutes)
        {
            var chip = new Button { Style = (Style)FindResource("Chip"), Content = $"{m}m", ToolTip = $"{m} menit" };
            chip.Click += (_, _) => _focus?.Countdown.SetDuration(TimeSpan.FromMinutes(m));
            Presets.Children.Add(chip);
        }
    }

    /// <summary>Tinggi modul: pilihan mode 30 + stadium 100 + baris tombol 42.</summary>
    public const double ContentHeight = 172;

    public void Attach(FocusController focus)
    {
        _focus = focus;
        Refresh();
    }

    public void Refresh()
    {
        if (_focus is not { } f) return;
        _syncing = true;
        SegPomodoro.IsChecked = f.Selected == TimerKind.Pomodoro;
        SegTimer.IsChecked = f.Selected == TimerKind.Countdown;
        SegStopwatch.IsChecked = f.Selected == TimerKind.Stopwatch;
        _syncing = false;

        SessionInfo.Visibility = f.Selected == TimerKind.Pomodoro ? Visibility.Visible : Visibility.Collapsed;
        switch (f.Selected)
        {
            case TimerKind.Countdown: RefreshCountdown(f.Countdown); break;
            case TimerKind.Stopwatch: RefreshStopwatch(f.Stopwatch); break;
            default: RefreshPomodoro(f.Timer); break;
        }
    }

    // ── Pomodoro ───────────────────────────────────────────────────────────

    private void RefreshPomodoro(FocusTimer t)
    {
        bool ready = t.Status == FocusStatus.Ready;
        bool paused = t.Status == FocusStatus.Paused;
        bool rest = t.Mode != FocusMode.Focus;
        var modeBrush = paused ? B("TextTertiary") : rest ? B("Rest") : B("Accent");

        // Label mode di dalam stadium.
        ModeLabel.Text = paused ? "DIJEDA"
            : ready && t.Mode == FocusMode.Focus ? "SIAP"
            : t.Mode switch { FocusMode.Focus => "FOKUS", FocusMode.LongBreak => "ISTIRAHAT PANJANG", _ => "ISTIRAHAT" };
        ModeLabel.Foreground = ready ? B("TextTertiary") : modeBrush;

        var remaining = t.Remaining;
        TimeText.Text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        TimeText.Foreground = paused ? B("TextSecondary") : B("TextPrimary");
        SetProgress(ready ? null : t.RemainingFraction, modeBrush);
        MinusButton.Visibility = PlusButton.Visibility = Visibility.Collapsed;
        Presets.Visibility = Visibility.Collapsed;
        SideText.Visibility = Visibility.Collapsed;

        // Titik sesi: selesai terisi (limau, juga saat istirahat), berjalan berupa cincin.
        SessionText.Text = $"SESI {t.Session} DARI {t.Settings.SessionsPerCycle}";
        Dots.Children.Clear();
        for (int i = 1; i <= t.Settings.SessionsPerCycle; i++)
        {
            bool done = i <= t.CompletedSessions;
            bool current = !done && i == t.Session && t.Mode == FocusMode.Focus && !ready;
            var dotColor = paused ? B("TextTertiary") : B("Accent");
            Dots.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Margin = new Thickness(i == 1 ? 0 : 6, 0, 0, 0),
                Fill = done ? dotColor : current ? Brushes.Transparent : B("Stroke"),
                Stroke = current ? dotColor : null,
                StrokeThickness = current ? 2 : 0,
            });
        }

        InfoText.Text = t.Status switch
        {
            FocusStatus.Running when t.EndsAt is { } end => $"Selesai pukul {end.ToLocalTime():HH:mm}",
            FocusStatus.Paused when t.PausedAt is { } at => PausedAgo(DateTimeOffset.UtcNow - at),
            _ => $"Fokus {t.Settings.FocusMinutes} · istirahat {t.Settings.ShortBreakMinutes} · panjang {t.Settings.LongBreakMinutes} menit",
        };
        TimeText.ToolTip = InfoText.Text;

        // Tombol: Siap → Mulai fokus (satu-satunya accent); berjalan → Jeda; dijeda → Lanjutkan.
        SkipButton.Tag = FindResource("Peeko.Icon.Skip");
        SetButtons(t.Status, skipVisible: t.Status == FocusStatus.Running);
        SkipButton.ToolTip = rest ? "Lewati istirahat" : "Lewati ke istirahat";
    }

    // ── Timer bebas ────────────────────────────────────────────────────────

    private void RefreshCountdown(CountdownTimer t)
    {
        bool ready = t.Status == FocusStatus.Ready;
        bool paused = t.Status == FocusStatus.Paused;
        var brush = paused ? B("TextTertiary") : B("Water");
        ModeLabel.Text = paused ? "DIJEDA" : ready ? "TIMER" : "BERJALAN";
        ModeLabel.Foreground = ready ? B("TextTertiary") : brush;
        TimeText.Text = FocusController.Format(t.Remaining);
        TimeText.Foreground = paused ? B("TextSecondary") : B("TextPrimary");
        TimeText.ToolTip = null;
        SetProgress(ready ? null : t.RemainingFraction, brush);

        MinusButton.Visibility = PlusButton.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        MinusButton.IsEnabled = t.Duration > TimeSpan.FromMinutes(1);
        Presets.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        SideText.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        SideText.Text = t.EndsAt is { } end ? $"Selesai {end.ToLocalTime():HH.mm}" : "Dijeda";

        // Berjalan: tombol kedua menambah 1 menit.
        SkipButton.Tag = FindResource("Peeko.Icon.Plus");
        SkipButton.ToolTip = "Tambah 1 menit";
        SetButtons(t.Status, skipVisible: !ready);
    }

    // ── Stopwatch ──────────────────────────────────────────────────────────

    private void RefreshStopwatch(StopwatchTimer sw)
    {
        bool ready = sw.Status == FocusStatus.Ready;
        bool paused = sw.Status == FocusStatus.Paused;
        var e = sw.Elapsed;
        ModeLabel.Text = paused ? "DIJEDA" : "STOPWATCH";
        ModeLabel.Foreground = B("TextTertiary");
        TimeText.Text = FocusController.Format(e);
        TimeText.Foreground = paused ? B("TextSecondary") : B("TextPrimary");
        TimeText.ToolTip = null;
        // Cincin berputar sekali tiap menit.
        SetProgress(ready ? null : paused ? 1 : Math.Max(0.001, e.Seconds / 60.0), B(paused ? "TextTertiary" : "TextPrimary"));

        MinusButton.Visibility = PlusButton.Visibility = Visibility.Collapsed;
        Presets.Visibility = Visibility.Collapsed;
        SideText.Visibility = Visibility.Visible;
        SideText.Text = sw.Laps.Count == 0 ? "" : $"Putaran {sw.Laps.Count} · {FocusController.Format(sw.Laps[^1])}";
        SideText.ToolTip = sw.Laps.Count == 0 ? null
            : string.Join("\n", sw.Laps.Select((l, i) => $"Putaran {i + 1}: {FocusController.Format(l)}"));

        SkipButton.Tag = FindResource("Peeko.Icon.Flag");
        SkipButton.ToolTip = "Catat putaran";
        SetButtons(sw.Status, skipVisible: sw.Status == FocusStatus.Running);
    }

    // ── Bersama ────────────────────────────────────────────────────────────

    private void SetButtons(FocusStatus status, bool skipVisible)
    {
        switch (status)
        {
            case FocusStatus.Ready:
                SetPrimary("Mulai", "Peeko.Fill.Play", accent: true);
                ResetButton.Visibility = Visibility.Collapsed;
                break;
            case FocusStatus.Running:
                SetPrimary("Jeda", "Peeko.Fill.Pause", accent: false);
                ResetButton.Visibility = Visibility.Visible;
                break;
            default:
                SetPrimary("Lanjutkan", "Peeko.Fill.Play", accent: true);
                ResetButton.Visibility = Visibility.Visible;
                break;
        }
        SkipButton.Visibility = skipVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Garis sisa di stadium; null = hanya track.</summary>
    private void SetProgress(double? fraction, Brush brush)
    {
        Progress.Stroke = brush;
        Progress.Visibility = fraction is null ? Visibility.Collapsed : Visibility.Visible;
        if (fraction is not { } f) return;
        double visible = Perimeter * Math.Clamp(f, 0, 1);
        Progress.StrokeDashArray = [visible / Stroke, Perimeter / Stroke];
        Progress.StrokeDashOffset = -(Perimeter - visible) / Stroke;
    }

    private void SetPrimary(string text, string icon, bool accent)
    {
        PrimaryButton.Content = text;
        PrimaryButton.Tag = FindResource(icon);
        PrimaryButton.Style = (Style)FindResource(accent ? "Peeko.Button.Pill" : "Peeko.Button.PillSecondary");
    }

    private static string PausedAgo(TimeSpan ago) =>
        ago < TimeSpan.FromMinutes(1) ? "Baru saja dijeda" : $"Dijeda {(int)ago.TotalMinutes} menit lalu";

    private void OnKind(object sender, RoutedEventArgs e)
    {
        if (_syncing || _focus is null) return;
        _focus.Selected = sender == SegTimer ? TimerKind.Countdown : sender == SegStopwatch ? TimerKind.Stopwatch : TimerKind.Pomodoro;
    }

    private void OnPrimary(object sender, RoutedEventArgs e)
    {
        if (_focus is not { } f) return;
        switch (f.Selected)
        {
            case TimerKind.Countdown:
                if (f.Countdown.Status == FocusStatus.Running) f.Countdown.Pause(); else f.Countdown.Start();
                break;
            case TimerKind.Stopwatch:
                if (f.Stopwatch.Status == FocusStatus.Running) f.Stopwatch.Pause(); else f.Stopwatch.Start();
                break;
            default:
                if (f.Timer.Status == FocusStatus.Running) f.Timer.Pause(); else f.Timer.Start();
                break;
        }
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        if (_focus is not { } f) return;
        switch (f.Selected)
        {
            case TimerKind.Countdown: f.Countdown.Reset(); break;
            case TimerKind.Stopwatch: f.Stopwatch.Reset(); break;
            default: f.Timer.Reset(); break;
        }
    }

    /// <summary>Tombol kedua: lewati (Pomodoro), +1 menit (timer), putaran (stopwatch).</summary>
    private void OnSkip(object sender, RoutedEventArgs e)
    {
        if (_focus is not { } f) return;
        switch (f.Selected)
        {
            case TimerKind.Countdown: f.Countdown.Add(TimeSpan.FromMinutes(1)); break;
            case TimerKind.Stopwatch: f.Stopwatch.Lap(); break;
            default: f.Timer.Skip(); break;
        }
    }

    private void OnMinus(object sender, RoutedEventArgs e) => _focus?.Countdown.SetDuration(_focus.Countdown.Duration - TimeSpan.FromMinutes(1));

    private void OnPlus(object sender, RoutedEventArgs e) => _focus?.Countdown.Add(TimeSpan.FromMinutes(1));

    /// <summary>Stadium mulai dari puncak tengah, searah jarum jam (inset setengah tebal garis).</summary>
    private static Geometry BuildStadium()
    {
        const double i = Stroke / 2, r = (H - Stroke) / 2;
        var g = Geometry.Parse(FormattableString.Invariant(
            $"M{W / 2},{i} H{W - i - r} A{r},{r} 0 0 1 {W - i - r},{H - i} H{i + r} A{r},{r} 0 0 1 {i + r},{i} Z"));
        g.Freeze();
        return g;
    }

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
