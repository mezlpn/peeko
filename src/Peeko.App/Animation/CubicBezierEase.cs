using System.Windows.Media.Animation;

namespace Peeko.App.Animation;

/// <summary>
/// Easing cubic-bezier persis seperti CSS. Berbeda dengan <see cref="KeySpline"/>, nilai Y boleh
/// di luar 0–1, jadi kurva pegas dari desain (mis. 0.34, 1.32, 0.64, 1) bisa dipakai apa adanya.
/// </summary>
public sealed class CubicBezierEase : IEasingFunction
{
    private readonly double _x1, _y1, _x2, _y2;

    public CubicBezierEase(double x1, double y1, double x2, double y2)
    {
        _x1 = x1; _y1 = y1; _x2 = x2; _y2 = y2;
    }

    public double Ease(double t)
    {
        if (t <= 0) return 0;
        if (t >= 1) return 1;
        return Bezier(SolveX(t), _y1, _y2);
    }

    private static double Bezier(double s, double p1, double p2)
    {
        double u = 1 - s;
        return 3 * u * u * s * p1 + 3 * u * s * s * p2 + s * s * s;
    }

    private static double BezierSlope(double s, double p1, double p2)
    {
        double u = 1 - s;
        return 3 * u * u * p1 + 6 * u * s * (p2 - p1) + 3 * s * s * (1 - p2);
    }

    /// <summary>Cari parameter s sehingga x(s) = x (Newton, fallback bisection).</summary>
    private double SolveX(double x)
    {
        double s = x;
        for (int i = 0; i < 6; i++)
        {
            double err = Bezier(s, _x1, _x2) - x;
            if (Math.Abs(err) < 1e-5) return s;
            double slope = BezierSlope(s, _x1, _x2);
            if (Math.Abs(slope) < 1e-6) break;
            s -= err / slope;
        }

        double lo = 0, hi = 1;
        s = x;
        for (int i = 0; i < 30; i++)
        {
            double v = Bezier(s, _x1, _x2);
            if (Math.Abs(v - x) < 1e-5) break;
            if (v < x) lo = s; else hi = s;
            s = (lo + hi) / 2;
        }
        return s;
    }
}
