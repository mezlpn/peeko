using System.Windows;
using System.Windows.Controls;

namespace Peeko.App.Music;

public partial class MusicPill : UserControl
{
    public MusicPill()
    {
        InitializeComponent();
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
