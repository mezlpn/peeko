using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Peeko.App.Animation;
using Peeko.App.Controls;

namespace Peeko.App.Music;

public partial class MusicModule : UserControl
{
    private const double FadeEdge = 24;
    private bool _marqueeActive;

    public MusicModule()
    {
        InitializeComponent();
        TitleText.SizeChanged += (_, _) => UpdateMarquee();
        TitleCanvas.SizeChanged += (_, _) => UpdateMarquee();
    }

    public SeekBar SeekBar => Seek;

    /// <summary>Tombol pin diklik; nilai = pin yang diminta.</summary>
    public event Action<bool>? PinToggled;

    private bool _pinned;

    public void SetPinned(bool pinned)
    {
        _pinned = pinned;
        if (pinned)
            PinButton.Foreground = (Brush)FindResource("Peeko.Brush.Accent");
        else
            PinButton.ClearValue(ForegroundProperty);
        PinButton.ToolTip = pinned ? "Lepas pin lirik" : "Pin lirik: tampilkan di notch saat mengecil";
    }

    private void OnPinClick(object sender, RoutedEventArgs e) => PinToggled?.Invoke(!_pinned);

    /// <summary>Level visualizer di samping chip sumber.</summary>
    public void SetLevels(double[] levels, bool playing) => Bars.SetLevels(levels, playing);

    /// <summary>Marquee hanya berjalan saat modul terlihat (hemat CPU).</summary>
    public void SetActive(bool active)
    {
        if (_marqueeActive == active) return;
        _marqueeActive = active;
        UpdateMarquee();
    }

    /// <summary>
    /// Judul terlalu panjang (Music.dc.html): diam 1,2 dtk, bergulir 30 px/dtk, berhenti 1,2 dtk
    /// di ujung, lalu kembali dengan fade. Tepi kanan memudar 24 px.
    /// </summary>
    private void UpdateMarquee()
    {
        TitleShift.BeginAnimation(TranslateTransform.XProperty, null);
        TitleText.BeginAnimation(OpacityProperty, null);
        TitleShift.X = 0;
        TitleText.Opacity = 1;

        double available = TitleCanvas.ActualWidth;
        double overflow = TitleText.ActualWidth - available;
        TitleCanvas.OpacityMask = overflow > 1 && available > FadeEdge ? EdgeFade(available) : null;
        if (overflow <= 1 || !_marqueeActive || Motion.Reduced) return;

        double distance = overflow + FadeEdge;
        double hold = 1.2, scroll = distance / 30, fade = 0.15;
        double end = hold + scroll + hold;
        var total = TimeSpan.FromSeconds(end + fade * 2);

        var x = new DoubleAnimationUsingKeyFrames { Duration = total, RepeatBehavior = RepeatBehavior.Forever };
        x.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        x.KeyFrames.Add(new LinearDoubleKeyFrame(0, Seconds(hold)));
        x.KeyFrames.Add(new LinearDoubleKeyFrame(-distance, Seconds(hold + scroll)));
        x.KeyFrames.Add(new LinearDoubleKeyFrame(-distance, Seconds(end + fade)));
        x.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, Seconds(end + fade)));
        Timeline.SetDesiredFrameRate(x, 30);

        var o = new DoubleAnimationUsingKeyFrames { Duration = total, RepeatBehavior = RepeatBehavior.Forever };
        o.KeyFrames.Add(new LinearDoubleKeyFrame(1, Seconds(end)));
        o.KeyFrames.Add(new LinearDoubleKeyFrame(0, Seconds(end + fade)));
        o.KeyFrames.Add(new LinearDoubleKeyFrame(1, Seconds(end + fade * 2)));
        Timeline.SetDesiredFrameRate(o, 30);

        TitleShift.BeginAnimation(TranslateTransform.XProperty, x);
        TitleText.BeginAnimation(OpacityProperty, o);

        static KeyTime Seconds(double s) => KeyTime.FromTimeSpan(TimeSpan.FromSeconds(s));
    }

    private static Brush EdgeFade(double width)
    {
        double start = (width - FadeEdge) / width;
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        brush.GradientStops.Add(new GradientStop(Colors.Black, 0));
        brush.GradientStops.Add(new GradientStop(Colors.Black, start));
        brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        brush.Freeze();
        return brush;
    }
}
