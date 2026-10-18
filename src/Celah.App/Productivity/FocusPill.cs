using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Celah.App.Animation;
using Celah.Core.Productivity;

namespace Celah.App.Productivity;

/// <summary>
/// Isi pil untuk timer fokus (Collapsed.dc.html): cincin 18 px (stroke 2.5) yang berkurang searah
/// jarum jam + sisa waktu. Versi ringkas (bersama musik): titik mode 6 px + waktu.
/// </summary>
public sealed class FocusPill : StackPanel
{
    private readonly Grid _ring = new() { Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center };
    private readonly Path _arc = new() { StrokeThickness = 2.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Ellipse _dot = new() { Width = 6, Height = 6, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _time = new() { VerticalAlignment = VerticalAlignment.Center };
    private bool _blinking;

    public FocusPill()
    {
        Orientation = Orientation.Horizontal;
        IsHitTestVisible = false;
        _ring.Children.Add(new Ellipse { StrokeThickness = 2.5, Stroke = B("Stroke"), Margin = new Thickness(0) });
        _ring.Children.Add(_arc);
        _time.Style = (Style)Application.Current.Resources["Celah.Text.Pill"];
        Children.Add(_ring);
        Children.Add(_dot);
        Children.Add(_time);
        SetCompact(false);
    }

    public void SetCompact(bool compact)
    {
        _ring.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _dot.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        _time.Margin = new Thickness(compact ? 6 : 10, 0, 0, 0);
    }

    public void Update(FocusTimer timer)
    {
        bool paused = timer.Status == FocusStatus.Paused;
        bool rest = timer.Mode != FocusMode.Focus;
        var color = paused ? B("TextTertiary") : rest ? B("Rest") : B("Accent");

        var remaining = timer.Remaining;
        _time.Text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        // Mode istirahat: waktu ikut amber supaya terbaca tanpa label.
        _time.Foreground = paused ? B("TextSecondary") : rest ? B("Rest") : B("TextPrimary");
        _dot.Fill = color;
        _arc.Stroke = color;
        _arc.Data = Arc(timer.RemainingFraction);

        // Satu menit terakhir: angka berkedip lembut (opacity 1 ↔ 0.6 tiap 1 dtk).
        bool blink = timer.Status == FocusStatus.Running && remaining < TimeSpan.FromMinutes(1) && !Motion.Reduced;
        if (blink == _blinking) return;
        _blinking = blink;
        if (blink)
        {
            var a = new DoubleAnimation(1, 0.6, TimeSpan.FromSeconds(0.5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = Motion.Sine };
            Timeline.SetDesiredFrameRate(a, 30);
            _time.BeginAnimation(OpacityProperty, a);
        }
        else
        {
            _time.BeginAnimation(OpacityProperty, null);
        }
    }

    public void StopAnimations()
    {
        _blinking = false;
        _time.BeginAnimation(OpacityProperty, null);
    }

    /// <summary>Busur dari puncak searah jarum jam sepanjang sisa waktu (r 7, pusat 9,9).</summary>
    private static Geometry Arc(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction >= 0.999) return new EllipseGeometry(new Point(9, 9), 7.75, 7.75);
        if (fraction <= 0.001) return Geometry.Empty;

        const double r = 7.75;
        double angle = fraction * 2 * Math.PI;
        var end = new Point(9 + r * Math.Sin(angle), 9 - r * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = new Point(9, 9 - r) };
        figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, true));
        var g = new PathGeometry([figure]);
        g.Freeze();
        return g;
    }

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Celah.Brush.{token}"];
}
