// Diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): komponen Visualizer,
// tiap bar mengejar level pitanya dengan pegas (600 / 30 / 0.5).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Peeko.App.Animation;

namespace Peeko.App.Controls;

/// <summary>Bar vertikal yang naik-turun mengikuti pita frekuensi lagu yang sedang diputar.</summary>
public sealed class Visualizer : StackPanel
{
    public static readonly DependencyProperty BarCountProperty = DependencyProperty.Register(
        nameof(BarCount), typeof(int), typeof(Visualizer), new PropertyMetadata(5, (d, _) => ((Visualizer)d).Build()));

    public static readonly DependencyProperty BarWidthProperty = DependencyProperty.Register(
        nameof(BarWidth), typeof(double), typeof(Visualizer), new PropertyMetadata(3.0, (d, _) => ((Visualizer)d).Build()));

    public static readonly DependencyProperty BarGapProperty = DependencyProperty.Register(
        nameof(BarGap), typeof(double), typeof(Visualizer), new PropertyMetadata(2.0, (d, _) => ((Visualizer)d).Build()));

    public static readonly DependencyProperty MaxBarHeightProperty = DependencyProperty.Register(
        nameof(MaxBarHeight), typeof(double), typeof(Visualizer), new PropertyMetadata(14.0, (d, _) => ((Visualizer)d).Build()));

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(Visualizer), new PropertyMetadata(Brushes.White, (d, _) => ((Visualizer)d).Recolor()));

    private readonly List<(Rectangle Bar, SpringValue Spring)> _bars = [];
    private double[] _levels = [];
    private bool _playing;

    public Visualizer()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
        Build();
    }

    public int BarCount { get => (int)GetValue(BarCountProperty); set => SetValue(BarCountProperty, value); }

    public double BarWidth { get => (double)GetValue(BarWidthProperty); set => SetValue(BarWidthProperty, value); }

    public double BarGap { get => (double)GetValue(BarGapProperty); set => SetValue(BarGapProperty, value); }

    public double MaxBarHeight { get => (double)GetValue(MaxBarHeightProperty); set => SetValue(MaxBarHeightProperty, value); }

    public Brush BarBrush { get => (Brush)GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }

    /// <summary>Level 0–1 per pita. Dijeda: semua bar turun jadi titik dan meredup.</summary>
    public void SetLevels(IReadOnlyList<double> levels, bool playing)
    {
        _levels = [.. levels];
        _playing = playing;
        for (int i = 0; i < _bars.Count; i++)
        {
            double level = i < levels.Count ? levels[i] : 0;
            // Seperti Bloom: minimal 20% saat diputar, 10% saat dijeda.
            double scale = playing ? Math.Max(0.2, Math.Clamp(level, 0, 1)) : 0.1;
            _bars[i].Spring.AnimateTo(Math.Max(BarWidth, scale * MaxBarHeight));
        }
        Opacity = playing ? 0.95 : 0.5;
    }

    private void Build()
    {
        foreach (var (_, spring) in _bars) spring.Set(spring.Target);
        Children.Clear();
        _bars.Clear();
        Height = MaxBarHeight;
        for (int i = 0; i < BarCount; i++)
        {
            var bar = new Rectangle
            {
                Width = BarWidth,
                Height = BarWidth,
                RadiusX = BarWidth / 2,
                RadiusY = BarWidth / 2,
                Fill = BarBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(i == 0 ? 0 : BarGap, 0, 0, 0),
            };
            Children.Add(bar);
            _bars.Add((bar, new SpringValue(BarWidth, SpringConfig.Bar, v => bar.Height = Math.Max(BarWidth, v))));
        }
        if (_levels.Length > 0) SetLevels(_levels, _playing);
    }

    private void Recolor()
    {
        foreach (var (bar, _) in _bars) bar.Fill = BarBrush;
    }
}
