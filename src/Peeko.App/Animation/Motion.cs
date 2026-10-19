using System.Windows;
using System.Windows.Media.Animation;

namespace Peeko.App.Animation;

/// <summary>
/// Token motion dari desain (Tokens.dc.html &amp; Motion.dc.html). Disimpan di C#, bukan XAML,
/// karena semua animasi notch dibangun dari kode.
/// </summary>
public static class Motion
{
    public static readonly TimeSpan Instant = Ms(90);
    public static readonly TimeSpan Fast = Ms(140);
    public static readonly TimeSpan Base = Ms(220);
    public static readonly TimeSpan Slow = Ms(320);
    public static readonly TimeSpan ShapeOpen = Ms(460);
    public static readonly TimeSpan ShapeClose = Ms(340);
    public static readonly TimeSpan PeekIn = Ms(300);
    public static readonly TimeSpan BlinkDown = Ms(70);
    public static readonly TimeSpan BlinkUp = Ms(90);
    public static readonly TimeSpan Breathe = Ms(4000);
    public static readonly TimeSpan ReducedCrossfade = Ms(120);

    public static readonly IEasingFunction Standard = new CubicBezierEase(0.2, 0, 0, 1);
    public static readonly IEasingFunction Out = new CubicBezierEase(0.16, 1, 0.3, 1);
    public static readonly IEasingFunction In = new CubicBezierEase(0.4, 0, 1, 1);
    public static readonly IEasingFunction Spring = new CubicBezierEase(0.34, 1.32, 0.64, 1);
    public static readonly IEasingFunction SpringSoft = new CubicBezierEase(0.3, 1.15, 0.55, 1);
    public static readonly IEasingFunction Sine = new CubicBezierEase(0.45, 0, 0.55, 1);

    /// <summary>"Tampilkan animasi di Windows" dimatikan → hanya crossfade singkat.</summary>
    public static bool Reduced => !SystemParameters.ClientAreaAnimation;

    public static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    public static DoubleAnimation To(double to, TimeSpan duration, IEasingFunction ease, double delayMs = 0, int fps = 60)
    {
        var a = new DoubleAnimation(to, duration)
        {
            EasingFunction = ease,
            BeginTime = Ms(delayMs),
        };
        Timeline.SetDesiredFrameRate(a, fps);
        return a;
    }

    public static void Animate(this IAnimatable target, DependencyProperty property, AnimationTimeline animation) =>
        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
}
