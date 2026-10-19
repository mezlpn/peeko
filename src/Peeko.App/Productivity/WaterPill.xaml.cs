using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Peeko.App.Animation;

namespace Peeko.App.Productivity;

public partial class WaterPill : UserControl
{
    public WaterPill()
    {
        InitializeComponent();
    }

    /// <summary>Chip "+1 gelas" diklik langsung dari pil.</summary>
    public event Action? DrinkClicked;

    /// <summary>Tetes berdenyut tiap 2,2 dtk selama pengingat tampil (±8 dtk).</summary>
    public void SetPulsing(bool on)
    {
        if (!on || Motion.Reduced)
        {
            DropScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            DropScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            return;
        }
        var pulse = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(2.2), RepeatBehavior = RepeatBehavior.Forever };
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.7)));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.18, KeyTime.FromPercent(0.8)) { EasingFunction = Motion.Spring });
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1)) { EasingFunction = Motion.Out });
        Timeline.SetDesiredFrameRate(pulse, 30);
        DropScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        DropScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
    }

    private void OnDrinkClick(object sender, RoutedEventArgs e) => DrinkClicked?.Invoke();
}
