using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Celah.App.Animation;

namespace Celah.App.Controls;

/// <summary>
/// Karakter idle "Kedip". Sesuai keputusan desain #1, tidak ada loop terus-menerus: hanya kedipan
/// acak tiap 4–9 dtk dan satu siklus napas setelah ada interaksi/event.
/// </summary>
public partial class Kedip : UserControl
{
    private readonly DispatcherTimer _blinkTimer;

    public Kedip()
    {
        InitializeComponent();
        _blinkTimer = new DispatcherTimer(DispatcherPriority.Background);
        _blinkTimer.Tick += (_, _) =>
        {
            Blink(twice: Random.Shared.NextDouble() < 0.2);
            ScheduleNextBlink();
        };
    }

    public void StartIdle()
    {
        if (_blinkTimer.IsEnabled) return;
        ScheduleNextBlink();
        _blinkTimer.Start();
    }

    public void StopIdle()
    {
        _blinkTimer.Stop();
        BreatheScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }

    private void ScheduleNextBlink() =>
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(Random.Shared.Next(4000, 9000));

    /// <summary>Kedip 160 ms: turun ke 0.12 (70 ms ease-in) lalu naik (90 ms ease-out).</summary>
    public void Blink(bool twice = false, double delayMs = 0)
    {
        var frames = new DoubleAnimationUsingKeyFrames { BeginTime = Motion.Ms(delayMs) };
        double t = 0;
        for (int i = 0; i < (twice ? 2 : 1); i++)
        {
            t += Motion.BlinkDown.TotalMilliseconds;
            frames.KeyFrames.Add(new EasingDoubleKeyFrame(0.12, KeyTime.FromTimeSpan(Motion.Ms(t))) { EasingFunction = Motion.In });
            t += Motion.BlinkUp.TotalMilliseconds;
            frames.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(Motion.Ms(t))) { EasingFunction = Motion.Out });
            t += 120; // jeda antar kedip ganda
        }
        Timeline.SetDesiredFrameRate(frames, 60);
        frames.Freeze();
        LeftBlink.Animate(ScaleTransform.ScaleYProperty, frames);
        RightBlink.Animate(ScaleTransform.ScaleYProperty, frames);
    }

    /// <summary>Satu siklus napas (scaleY 1 → 0.8 → 1, 4 dtk) lalu berhenti.</summary>
    public void BreatheOnce()
    {
        if (Motion.Reduced) return;
        var a = new DoubleAnimation(1, 0.8, TimeSpan.FromMilliseconds(Motion.Breathe.TotalMilliseconds / 2))
        {
            AutoReverse = true,
            EasingFunction = Motion.Sine,
        };
        Timeline.SetDesiredFrameRate(a, 30);
        BreatheScale.Animate(ScaleTransform.ScaleYProperty, a);
    }

    /// <summary>Melirik ±3 px; <paramref name="direction"/> antara -1 (kiri) dan 1 (kanan).</summary>
    public void Look(double direction)
    {
        if (Motion.Reduced) return;
        double x = Math.Clamp(direction, -1, 1) * 3;
        LookShift.Animate(TranslateTransform.XProperty, Motion.To(x, Motion.Ms(200), Motion.Out, fps: 30));
    }
}
