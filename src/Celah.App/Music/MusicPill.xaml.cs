using System.Windows.Controls;
using System.Windows.Shapes;
using Celah.App.Animation;

namespace Celah.App.Music;

public partial class MusicPill : UserControl
{
    private readonly Rectangle[] _bars;

    public MusicPill()
    {
        InitializeComponent();
        _bars = [Bar0, Bar1, Bar2, Bar3];
    }

    /// <summary>
    /// Level 0–1 per bar. Semua nol (dijeda) → bar turun menjadi titik 3 px dalam 220 ms;
    /// selain itu tinggi 4–14 px di-tween 80 ms.
    /// </summary>
    public void SetLevels(double[] levels)
    {
        bool silent = levels.All(l => l <= 0);
        for (int i = 0; i < _bars.Length; i++)
        {
            double level = i < levels.Length ? levels[i] : 0;
            double height = silent ? 3 : 4 + Math.Clamp(level, 0, 1) * 10;
            var duration = silent ? Motion.Base : Motion.Ms(80);
            _bars[i].Animate(HeightProperty, Motion.To(height, duration, silent ? Motion.Standard : Motion.Out, fps: 30));
        }
    }
}
