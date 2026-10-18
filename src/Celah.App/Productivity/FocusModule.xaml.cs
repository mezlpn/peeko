using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Celah.App.Animation;
using Celah.Core.Productivity;

namespace Celah.App.Productivity;

public partial class FocusModule : UserControl
{
    private const double W = 236, H = 120, Stroke = 4;
    private static readonly Geometry Stadium = BuildStadium();
    private static readonly double Perimeter = 2 * (W - H) + Math.PI * (H - Stroke);

    private FocusTimer? _timer;

    public FocusModule()
    {
        InitializeComponent();
        Track.Data = Stadium;
        Progress.Data = Stadium;
    }

    public void Attach(FocusTimer timer)
    {
        _timer = timer;
        Refresh();
    }

    public void Refresh()
    {
        if (_timer is not { } t) return;

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

        // Garis sisa waktu. Siap: hanya track tanpa isi.
        Progress.Stroke = modeBrush;
        Progress.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        double visible = Perimeter * Math.Clamp(t.RemainingFraction, 0, 1);
        Progress.StrokeDashArray = [visible / Stroke, Perimeter / Stroke];
        Progress.StrokeDashOffset = -(Perimeter - visible) / Stroke;

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

        // Tombol: Siap → Mulai fokus (satu-satunya accent); berjalan → Jeda; dijeda → Lanjutkan.
        switch (t.Status)
        {
            case FocusStatus.Ready:
                SetPrimary("Mulai fokus", "Celah.Fill.Play", accent: true);
                ResetButton.Visibility = Visibility.Collapsed;
                SkipButton.Visibility = Visibility.Collapsed;
                break;
            case FocusStatus.Running:
                SetPrimary("Jeda", "Celah.Fill.Pause", accent: false);
                ResetButton.Visibility = Visibility.Visible;
                SkipButton.Visibility = Visibility.Visible;
                SkipButton.ToolTip = rest ? "Lewati istirahat" : "Lewati ke istirahat";
                break;
            default:
                SetPrimary("Lanjutkan", "Celah.Fill.Play", accent: true);
                ResetButton.Visibility = Visibility.Visible;
                SkipButton.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void SetPrimary(string text, string icon, bool accent)
    {
        PrimaryButton.Content = text;
        PrimaryButton.Tag = FindResource(icon);
        PrimaryButton.Style = (Style)FindResource(accent ? "Celah.Button.Pill" : "Celah.Button.PillSecondary");
    }

    private static string PausedAgo(TimeSpan ago) =>
        ago < TimeSpan.FromMinutes(1) ? "Baru saja dijeda" : $"Dijeda {(int)ago.TotalMinutes} menit lalu";

    private void OnPrimary(object sender, RoutedEventArgs e)
    {
        if (_timer is null) return;
        if (_timer.Status == FocusStatus.Running) _timer.Pause();
        else _timer.Start();
    }

    private void OnReset(object sender, RoutedEventArgs e) => _timer?.Reset();

    private void OnSkip(object sender, RoutedEventArgs e) => _timer?.Skip();

    /// <summary>Stadium mulai dari puncak tengah, searah jarum jam (inset setengah tebal garis).</summary>
    private static Geometry BuildStadium()
    {
        const double i = Stroke / 2, r = (H - Stroke) / 2;
        var g = Geometry.Parse(FormattableString.Invariant(
            $"M{W / 2},{i} H{W - i - r} A{r},{r} 0 0 1 {W - i - r},{H - i} H{i + r} A{r},{r} 0 0 1 {i + r},{i} Z"));
        g.Freeze();
        return g;
    }

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Celah.Brush.{token}"];
}
