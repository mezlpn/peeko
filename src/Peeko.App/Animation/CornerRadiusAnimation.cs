using System.Windows;
using System.Windows.Media.Animation;

namespace Peeko.App.Animation;

/// <summary>WPF tidak punya animasi bawaan untuk CornerRadius; dipakai untuk radius bawah notch.</summary>
public sealed class CornerRadiusAnimation : AnimationTimeline
{
    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(CornerRadius), typeof(CornerRadiusAnimation));

    public static readonly DependencyProperty EasingFunctionProperty = DependencyProperty.Register(
        nameof(EasingFunction), typeof(IEasingFunction), typeof(CornerRadiusAnimation));

    public CornerRadius To
    {
        get => (CornerRadius)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction
    {
        get => (IEasingFunction?)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    public override Type TargetPropertyType => typeof(CornerRadius);

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        var from = (CornerRadius)defaultOriginValue;
        double t = animationClock.CurrentProgress ?? 0;
        if (EasingFunction is { } ease) t = ease.Ease(t);

        // Radius negatif tidak valid; overshoot pegas dijepit di 0.
        static double Lerp(double a, double b, double t) => Math.Max(0, a + (b - a) * t);
        return new CornerRadius(
            Lerp(from.TopLeft, To.TopLeft, t),
            Lerp(from.TopRight, To.TopRight, t),
            Lerp(from.BottomRight, To.BottomRight, t),
            Lerp(from.BottomLeft, To.BottomLeft, t));
    }

    protected override Freezable CreateInstanceCore() => new CornerRadiusAnimation();
}
