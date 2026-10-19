using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Celah.App.Controls;

/// <summary>Stepper angka "− 25 mnt +" (Settings.dc.html): tinggi 30, radius 6.</summary>
public sealed class Stepper : Border
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int), typeof(Stepper),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Stepper)d).UpdateText()));

    private readonly TextBlock _text = new() { MinWidth = 56, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
    private readonly Button _minus;
    private readonly Button _plus;

    public Stepper()
    {
        Height = 30;
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        Background = (Brush)Application.Current.Resources["Celah.Brush.WinBg"];
        BorderBrush = (Brush)Application.Current.Resources["Celah.Brush.WinStroke"];
        _text.Foreground = (Brush)Application.Current.Resources["Celah.Brush.TextPrimary"];

        _minus = MakeButton("−", -1);
        _plus = MakeButton("+", 1);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_minus);
        row.Children.Add(_text);
        row.Children.Add(_plus);
        Child = row;
        UpdateText();
        // Suffix/Minimum/Maximum diisi dari XAML setelah konstruktor.
        Loaded += (_, _) => UpdateText();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Minimum { get; set; }
    public int Maximum { get; set; } = 100;
    public int Step { get; set; } = 1;
    public string Suffix { get; set; } = "";

    private Button MakeButton(string label, int direction)
    {
        var button = new Button
        {
            Width = 28,
            Height = 28,
            Content = new TextBlock { Text = label, FontSize = 15 },
            Style = (Style)Application.Current.Resources["Celah.Button.Flat"],
            Tag = new CornerRadius(5),
        };
        button.Click += (_, _) =>
        {
            Value = Math.Clamp(Value + direction * Step, Minimum, Maximum);
            GetBindingExpression(ValueProperty)?.UpdateSource();
        };
        return button;
    }

    private void UpdateText()
    {
        _text.Text = $"{Value} {Suffix}".Trim();
        _minus.IsEnabled = Value > Minimum;
        _plus.IsEnabled = Value < Maximum;
    }
}
