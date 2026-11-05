using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Peeko.Core.Prayer;

namespace Peeko.App.Prayer;

/// <summary>Isi pil beberapa menit sebelum waktu salat: ikon kubah amber · nama salat · sisa menit.</summary>
public sealed class PrayerPill : StackPanel
{
    private readonly TextBlock _name = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly TextBlock _left = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };

    public PrayerPill()
    {
        Orientation = Orientation.Horizontal;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
        var res = Application.Current.Resources;
        Children.Add(new Path
        {
            Data = (Geometry)res["Peeko.Icon.Prayer"],
            Stroke = (Brush)res["Peeko.Brush.Rest"],
            StrokeThickness = 1.9,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Width = 24,
            Height = 24,
            LayoutTransform = new ScaleTransform(0.66, 0.66),
            VerticalAlignment = VerticalAlignment.Center,
        });
        _name.Style = (Style)res["Peeko.Text.Pill"];
        _left.Style = (Style)res["Peeko.Text.Pill"];
        _left.Foreground = (Brush)res["Peeko.Brush.Rest"];
        Children.Add(_name);
        Children.Add(_left);
    }

    public void Update(PrayerTime? next)
    {
        if (next is null) return;
        _name.Text = next.Label;
        _left.Text = PrayerText.Left(next.At - DateTime.Now, compact: true);
    }
}

/// <summary>Teks sisa waktu yang dipakai pil, tab, dan peek.</summary>
public static class PrayerText
{
    /// <summary>"12 mnt", "1 j 5 mnt" (ringkas) atau "1 jam 5 menit lagi".</summary>
    public static string Left(TimeSpan left, bool compact = false)
    {
        int minutes = Math.Max(0, (int)Math.Ceiling(left.TotalMinutes));
        int h = minutes / 60, m = minutes % 60;
        if (compact) return h > 0 ? $"{h} j {m} mnt" : minutes == 0 ? "sekarang" : $"{m} mnt";
        if (minutes == 0) return "Sudah masuk waktu";
        return h > 0 ? (m > 0 ? $"{h} jam {m} menit lagi" : $"{h} jam lagi") : $"{m} menit lagi";
    }
}
