using System.Windows;
using System.Windows.Controls;
using Peeko.App.Animation;

namespace Peeko.App.Music;

public partial class MusicPill : UserControl
{
    /// <summary>Batas lebar teks lirik di pil; baris lebih panjang diberi elipsis.</summary>
    public const double MaxLyricWidth = 380;
    private const double SidePadding = 16;

    public MusicPill()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Pin lirik: tampilkan baris yang sedang diputar (null → pil biasa). Baris baru muncul dengan
    /// fade singkat. Mengembalikan lebar pil yang dibutuhkan (0 tanpa lirik).
    /// </summary>
    public double SetLyric(string? text)
    {
        if (text is null)
        {
            LyricText.Visibility = Visibility.Collapsed;
            LyricText.Text = "";
            return 0;
        }

        bool changed = LyricText.Text != text;
        LyricText.Text = text;
        LyricText.Visibility = Visibility.Visible;
        if (changed && !Motion.Reduced)
        {
            LyricText.Opacity = 0;
            LyricText.Animate(OpacityProperty, Motion.To(1, Motion.Base, Motion.Out));
        }

        Root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Root.DesiredSize.Width + SidePadding * 2;
    }

    /// <summary>
    /// Bersama timer fokus: musik pindah ke kiri pil dengan tiga bar (Collapsed.dc.html → gabungan).
    /// </summary>
    public void SetCompact(bool compact)
    {
        Root.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        Root.Margin = new Thickness(compact ? 8 : 0, 0, 0, 0);
        Bars.BarCount = compact ? 3 : 5;
    }

    /// <summary>Level 0–1 per pita frekuensi; <paramref name="playing"/> false → bar turun jadi titik.</summary>
    public void SetLevels(double[] levels, bool playing) => Bars.SetLevels(levels, playing);
}
