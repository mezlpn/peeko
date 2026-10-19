using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Celah.App.Animation;
using Celah.Core.Productivity;

namespace Celah.App.Productivity;

public partial class WaterModule : UserControl
{
    private WaterController? _water;
    private int _shownGlasses = -1;
    private bool _wasReached;

    public WaterModule()
    {
        InitializeComponent();
    }

    public void Attach(WaterController water)
    {
        _water = water;
        Refresh();
    }

    public void Refresh()
    {
        if (_water is null) return;
        var t = _water.Tracker;
        int glasses = t.Glasses;
        bool reached = t.GoalReached;
        var d = t.Data;

        CountText.Text = glasses.ToString();
        GoalText.Text = $"/ {t.Goal} gelas";
        CountView.Visibility = reached ? Visibility.Collapsed : Visibility.Visible;
        DoneView.Visibility = reached ? Visibility.Visible : Visibility.Collapsed;
        DoneDetail.Text = $"{glasses} gelas · {WaterTracker.Liters(glasses * d.GlassMl)}";

        // Gelas di atas target tetap dicatat, tapi tidak menganimasikan ulang gelas terakhir;
        // kelebihannya tampil sebagai "+n" di samping gelas.
        BuildGlasses(Math.Min(glasses, t.Goal), t.Goal,
            animateNew: glasses == _shownGlasses + 1 && _shownGlasses >= 0 && glasses <= t.Goal);
        if (glasses > t.Goal)
        {
            Glasses.Children.Add(new TextBlock
            {
                Text = $"+{glasses - t.Goal}",
                Style = (Style)FindResource("Celah.Text.BodyStrong"),
                Foreground = B("Water"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            });
        }
        if (reached && !_wasReached && _shownGlasses >= 0) Wave();
        _shownGlasses = glasses;
        _wasReached = reached;

        if (reached)
        {
            ReminderText.Text = "Pengingat dijeda sampai besok";
            ReminderDetail.Text = "Gelas tambahan tetap dicatat";
        }
        else
        {
            ReminderText.Text = t.NextReminder is { } next ? $"Pengingat berikutnya {next.ToLocalTime():HH:mm}" : "Pengingat aktif";
            ReminderDetail.Text = $"Setiap {d.IntervalMinutes} menit · {WaterTracker.Liters(glasses * d.GlassMl)} dari {WaterTracker.Liters(t.Goal * d.GlassMl)}";
        }

        // Tombol utama memakai warna air; setelah target tercapai turun jadi varian sekunder.
        DrinkButton.Background = B(reached ? "WaterMuted" : "Water");
        DrinkButton.Foreground = reached ? B("Water") : new SolidColorBrush(Color.FromRgb(0x04, 0x14, 0x1C));
        UndoButton.IsEnabled = glasses > 0;
    }

    /// <summary>Delapan gelas 22 × 36; gelas yang baru terisi naik dari bawah (320 ms ease-spring).</summary>
    private void BuildGlasses(int filled, int goal, bool animateNew)
    {
        Glasses.Children.Clear();
        for (int i = 0; i < goal; i++)
        {
            bool full = i < filled;
            var glass = new Border
            {
                Width = 22,
                Height = 36,
                Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 0),
                CornerRadius = new CornerRadius(4, 4, 8, 8),
                BorderThickness = new Thickness(full ? 0 : 1.5),
                BorderBrush = B("StrokeStrong"),
                ClipToBounds = true,
                RenderTransformOrigin = new Point(0.5, 1),
                RenderTransform = new TranslateTransform(),
            };
            if (full)
            {
                var water = new Border
                {
                    Background = B("Water"),
                    CornerRadius = new CornerRadius(4, 4, 8, 8),
                    RenderTransformOrigin = new Point(0.5, 1),
                    RenderTransform = new ScaleTransform(1, 1),
                };
                glass.Child = water;
                if (animateNew && i == filled - 1 && !Motion.Reduced)
                {
                    var scale = (ScaleTransform)water.RenderTransform;
                    var a = Motion.To(1, Motion.Ms(320), Motion.Spring);
                    a.From = 0;
                    scale.Animate(ScaleTransform.ScaleYProperty, a);
                }
            }
            Glasses.Children.Add(glass);
        }
    }

    /// <summary>Target tercapai: gelas "bergelombang" sekali (stagger 40 ms, Y −3 → 0).</summary>
    private void Wave()
    {
        if (Motion.Reduced) return;
        int i = 0;
        foreach (var glass in Glasses.Children.OfType<Border>())
        {
            var shift = (TranslateTransform)glass.RenderTransform;
            var a = Motion.To(0, Motion.Base, Motion.Out, 40 * i++);
            a.From = -3;
            shift.Animate(TranslateTransform.YProperty, a);
        }
    }

    private void OnDrink(object sender, RoutedEventArgs e) => _water?.Drink();

    private void OnUndo(object sender, RoutedEventArgs e) => _water?.Undo();

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Celah.Brush.{token}"];
}
