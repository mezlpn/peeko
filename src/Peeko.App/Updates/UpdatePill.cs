using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Peeko.App.Animation;

namespace Peeko.App.Updates;

/// <summary>
/// Pil "Update tersedia" yang menggantikan mata Kedip. Sengaja agak mengganggu: tiap 20 dtk pil
/// bergoyang dan panahnya memantul. Animasinya sekali jalan (bukan loop), jadi CPU tetap ≈ 0%.
/// </summary>
public sealed class UpdatePill : Grid
{
    private static readonly Geometry Arrow = Geometry.Parse("M12,19 V5 M6,11 L12,5 L18,11");

    private readonly TranslateTransform _shake = new();
    private readonly TranslateTransform _bounce = new();
    private readonly TextBlock _text = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Border _progressTrack;
    private readonly Border _progressFill;
    private readonly Button _button;
    private readonly DispatcherTimer _nag = new() { Interval = TimeSpan.FromSeconds(20) };
    private UpdateState _state;

    public UpdatePill()
    {
        Height = 32;
        Margin = new Thickness(10, 0, 4, 0);
        RenderTransform = _shake;

        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        left.Children.Add(new Path
        {
            Data = Arrow,
            Stroke = B("Accent"),
            StrokeThickness = 2.4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Width = 24,
            Height = 24,
            LayoutTransform = new ScaleTransform(0.6667, 0.6667),
            RenderTransform = _bounce,
            VerticalAlignment = VerticalAlignment.Center,
        });
        _text.Style = (Style)Application.Current.Resources["Peeko.Text.Pill"];
        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(_text);
        _progressTrack = new Border
        {
            Height = 3,
            Width = 110,
            Margin = new Thickness(8, 3, 0, 0),
            CornerRadius = new CornerRadius(1.5),
            Background = B("Stroke"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed,
        };
        _progressFill = new Border { CornerRadius = new CornerRadius(1.5), Background = B("Accent"), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
        _progressTrack.Child = _progressFill;
        textStack.Children.Add(_progressTrack);
        left.Children.Add(textStack);
        Children.Add(left);

        _button = new Button
        {
            Style = (Style)Application.Current.Resources["Peeko.Button.Pill"],
            Height = 24,
            Padding = new Thickness(12, 0, 12, 0),
            Content = "Pasang",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _button.Click += (_, _) => InstallClicked?.Invoke();
        Children.Add(_button);

        _nag.Tick += (_, _) => Nag();
    }

    /// <summary>Tombol "Pasang"/"Coba lagi" diklik.</summary>
    public event Action? InstallClicked;

    public void Update(UpdateState state, string version, double progress)
    {
        _state = state;
        _text.Text = state switch
        {
            UpdateState.Downloading => $"Mengunduh {Math.Round(progress * 100)}%",
            UpdateState.Installing => "Memasang…",
            UpdateState.Failed => "Update gagal",
            _ => $"Update {version}",
        };
        _progressTrack.Visibility = state == UpdateState.Downloading ? Visibility.Visible : Visibility.Collapsed;
        _progressFill.Width = _progressTrack.Width * Math.Clamp(progress, 0, 1);
        _button.Visibility = state is UpdateState.Available or UpdateState.Failed ? Visibility.Visible : Visibility.Collapsed;
        _button.Content = state == UpdateState.Failed ? "Coba lagi" : "Pasang";
    }

    /// <summary>Goyangan berkala hanya saat pil terlihat dan update belum dipasang.</summary>
    public void SetVisible(bool visible)
    {
        if (visible && !_nag.IsEnabled)
        {
            _nag.Start();
            Nag();
        }
        else if (!visible)
        {
            _nag.Stop();
        }
    }

    private void Nag()
    {
        if (_state != UpdateState.Available || Motion.Reduced) return;

        // Pil bergoyang kiri-kanan (±4 px, 450 ms).
        var shake = new DoubleAnimationUsingKeyFrames { Duration = Motion.Ms(450) };
        double[] xs = [0, -4, 4, -3, 3, -1, 0];
        for (int i = 0; i < xs.Length; i++)
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(xs[i], KeyTime.FromPercent(i / (double)(xs.Length - 1))) { EasingFunction = Motion.Sine });
        Timeline.SetDesiredFrameRate(shake, 60);
        _shake.BeginAnimation(TranslateTransform.XProperty, shake);

        // Panah memantul dua kali (−4 px).
        var bounce = new DoubleAnimationUsingKeyFrames { Duration = Motion.Ms(700) };
        double[] ys = [0, -4, 0, -3, 0];
        for (int i = 0; i < ys.Length; i++)
            bounce.KeyFrames.Add(new EasingDoubleKeyFrame(ys[i], KeyTime.FromPercent(i / (double)(ys.Length - 1))) { EasingFunction = i % 2 == 1 ? Motion.Out : Motion.In });
        Timeline.SetDesiredFrameRate(bounce, 60);
        _bounce.BeginAnimation(TranslateTransform.YProperty, bounce);
    }

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
