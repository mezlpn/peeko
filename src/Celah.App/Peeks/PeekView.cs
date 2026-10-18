using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Celah.App.Animation;
using Celah.Core.Peeks;

namespace Celah.App.Peeks;

/// <summary>
/// Isi satu peek (Peek.dc.html): ikon 18 · label atau bar · nilai, tinggi 44, padding kiri 16.
/// Dipakai juga sebagai baris 32 px di bawah bar tab saat panel terbuka (<see cref="Compact"/>).
/// </summary>
public sealed class PeekView : Grid
{
    private const double BarWidthEstimate = 232;

    // Ikon garis, kanvas 24 × 24, stroke 1.8 (dari SVG di desain).
    private static readonly Geometry VolumeIcon = Geometry.Parse("M4,9.5 H7.5 L12,5.5 V18.5 L7.5,14.5 H4 Z M15.5,9 A4,4 0 0 1 15.5,15 M18,6.5 A7.5,7.5 0 0 1 18,17.5");
    private static readonly Geometry MuteIcon = Geometry.Parse("M4,9.5 H7.5 L12,5.5 V18.5 L7.5,14.5 H4 Z M16,9.5 L21,14.5 M21,9.5 L16,14.5");
    private static readonly Geometry SunIcon = Geometry.Parse("M8.5,12 A3.5,3.5 0 1 0 15.5,12 A3.5,3.5 0 1 0 8.5,12 M12,3 V5 M12,19 V21 M3,12 H5 M19,12 H21 M5.6,5.6 L7,7 M17,17 L18.4,18.4 M5.6,18.4 L7,17 M17,7 L18.4,5.6");
    private static readonly Geometry CapsIcon = Geometry.Parse("M12,4.5 L19,11.5 H15 V15.5 H9 V11.5 H5 Z M9,19.5 H15");
    private static readonly Geometry KeyboardIcon = Geometry.Parse("M5,6.5 H19 A2,2 0 0 1 21,8.5 V15.5 A2,2 0 0 1 19,17.5 H5 A2,2 0 0 1 3,15.5 V8.5 A2,2 0 0 1 5,6.5 Z M7,10 H7.01 M10.5,10 H10.51 M14,10 H14.01 M17,10 H17.01 M8,14 H16");
    private static readonly Geometry ArrowIcon = Geometry.Parse("M5,12 H19 M13,6 L19,12 L13,18");
    private static readonly Geometry WifiIcon = Geometry.Parse("M3.5,9.5 A12,12 0 0 1 20.5,9.5 M6.5,12.8 A7.5,7.5 0 0 1 17.5,12.8 M9.5,16 A3,3 0 0 1 14.5,16 M12,19 H12.01");
    private static readonly Geometry SlashIcon = Geometry.Parse("M4,4 L20,20");
    private static readonly Geometry HeadphoneIcon = Geometry.Parse("M4,15 V12 A8,8 0 0 1 20,12 V15 M5,14 H6 A1.5,1.5 0 0 1 7.5,15.5 V18.5 A1.5,1.5 0 0 1 6,20 H5 A1.5,1.5 0 0 1 3.5,18.5 V15.5 A1.5,1.5 0 0 1 5,14 Z M18,14 H19 A1.5,1.5 0 0 1 20.5,15.5 V18.5 A1.5,1.5 0 0 1 19,20 H18 A1.5,1.5 0 0 1 16.5,18.5 V15.5 A1.5,1.5 0 0 1 18,14 Z");
    private static readonly Geometry BoltIcon = Geometry.Parse("M13,2 L5,13.5 H11 L10,22 L18,10.5 H12 Z");
    private static readonly Geometry HappyEye = Geometry.Parse("M2,10 Q8,0 14,10");

    private Border? _fill;
    private PeekKind? _kind;

    public PeekView()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>Versi baris 32 px di dalam panel expanded.</summary>
    public bool Compact { get; set; }

    public PeekKind? Kind => _kind;

    /// <summary>
    /// Tampilkan isi peek. Peek sejenis (volume ↔ volume) hanya memperbarui nilai: bar bergerak
    /// 160 ms ease-standard (Peek.dc.html → aturan antrian).
    /// </summary>
    public void Show(PeekRequest r)
    {
        bool sameLevelKind = _kind == r.Kind && _fill is not null && r.Kind is PeekKind.Volume or PeekKind.Brightness;
        double previousFill = _fill?.Width ?? 0;
        _kind = r.Kind;

        Children.Clear();
        _fill = null;
        Margin = Compact ? new Thickness(12, 0, 12, 0) : new Thickness(16, 0, HasChip(r) ? 10 : 18, 0);

        switch (r.Kind)
        {
            case PeekKind.Volume:
            case PeekKind.Brightness:
            {
                bool muted = r.Kind == PeekKind.Volume && r.Flag;
                var icon = r.Kind == PeekKind.Brightness ? SunIcon : muted ? MuteIcon : VolumeIcon;
                AddIcon(icon, muted ? "TextSecondary" : "TextPrimary");
                AddBar(r.Level, muted, sameLevelKind ? previousFill : null);
                AddValue(muted ? "Bisu" : $"{Math.Round(r.Level * 100)}", "TextPrimary", width: muted ? double.NaN : 24);
                break;
            }
            case PeekKind.CapsLock:
                AddIcon(CapsIcon, r.Flag ? "Accent" : "TextTertiary");
                AddLabel("Caps Lock");
                AddChip(r.Flag ? "AKTIF" : "MATI", r.Flag ? "Accent" : "TextSecondary", r.Flag ? "AccentMuted" : "SurfaceRaised");
                break;
            case PeekKind.Language:
                AddIcon(KeyboardIcon, "TextPrimary");
                AddLanguageSwitch(r.Detail ?? "", r.Text ?? "");
                AddValue(r.Caption ?? "", "TextSecondary", small: true);
                break;
            case PeekKind.BatteryLow:
                AddBattery(r.Percent ?? 0, "Warning");
                AddLabel("Baterai lemah");
                AddValue($"{r.Percent}%", "Warning");
                break;
            case PeekKind.BatteryCritical:
                AddBattery(r.Percent ?? 0, "Danger");
                AddLabel("Colok charger sekarang");
                AddChip($"{r.Percent}%", "Danger", "DangerMuted");
                break;
            case PeekKind.Charging:
                AddHappyEyes();
                AddLabel("Mengisi daya");
                AddBoltValue(r.Percent ?? 0);
                break;
            case PeekKind.Discharging:
                AddBattery(r.Percent ?? 0, "TextSecondary");
                AddLabel("Charger dilepas");
                AddValue(r.Detail is null ? $"{r.Percent}%" : $"{r.Percent}% · {r.Detail}", "TextSecondary", small: true);
                break;
            case PeekKind.WifiConnected:
                AddIcon(WifiIcon, "Accent");
                AddLabel("Tersambung");
                AddValue(r.Text ?? "", "TextSecondary", small: true, maxWidth: 120);
                break;
            case PeekKind.WifiDisconnected:
                AddIcon(WifiIcon, "TextTertiary", overlay: SlashIcon, overlayBrush: "Danger");
                AddLabel("Wi-Fi terputus");
                AddValue(r.Text ?? "", "TextTertiary", small: true, maxWidth: 120);
                break;
            case PeekKind.AudioDeviceConnected:
                AddIcon(r.Flag ? HeadphoneIcon : VolumeIcon, "Accent");
                AddTwoLine(r.Text ?? "Perangkat audio", "Tersambung");
                break;
            case PeekKind.AudioDeviceDisconnected:
                AddIcon(r.Flag ? HeadphoneIcon : VolumeIcon, "TextTertiary");
                AddTwoLine(r.Text ?? "Perangkat audio", "Terputus");
                break;
            case PeekKind.FocusDone:
                AddHappyEyes();
                AddLabel("Sesi selesai");
                AddValue(r.Detail ?? "", "TextSecondary", small: true);
                break;
            case PeekKind.BreakDone:
                AddIcon((Geometry)Application.Current.Resources["Celah.Icon.Focus"], "Accent");
                AddLabel("Istirahat selesai");
                AddValue(r.Detail ?? "", "TextSecondary", small: true);
                break;
            case PeekKind.ShelfDrop:
                AddIcon((Geometry)Application.Current.Resources["Celah.Icon.Shelf"], "TextPrimary");
                AddLabel(r.Flag ? "Tersimpan di rak" : "Simpan di rak");
                AddValue(r.Text ?? "", "TextSecondary", small: true);
                break;
        }
    }

    private static bool HasChip(PeekRequest r) => r.Kind is PeekKind.CapsLock or PeekKind.BatteryCritical;

    // ── Bagian ──────────────────────────────────────────────────────────────

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Celah.Brush.{token}"];

    private void Place(UIElement e, int column)
    {
        SetColumn(e, column);
        Children.Add(e);
    }

    private void AddIcon(Geometry geometry, string brush, Geometry? overlay = null, string? overlayBrush = null)
    {
        var host = new Grid { Width = 18, Height = 18, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        host.Children.Add(IconPath(geometry, B(brush)));
        if (overlay is not null) host.Children.Add(IconPath(overlay, B(overlayBrush!)));
        Place(host, 0);
    }

    private static Path IconPath(Geometry geometry, Brush stroke) => new()
    {
        Data = geometry,
        Stroke = stroke,
        StrokeThickness = 1.8,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        Width = 24,
        Height = 24,
        LayoutTransform = new ScaleTransform(0.75, 0.75),
    };

    private void AddLabel(string text) => Place(new TextBlock
    {
        Text = text,
        Style = (Style)Application.Current.Resources["Celah.Text.Pill"],
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    }, 1);

    private void AddTwoLine(string name, string status)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = name,
            Style = (Style)Application.Current.Resources["Celah.Text.Pill"],
            LineHeight = 15,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!Compact)
        {
            stack.Children.Add(new TextBlock
            {
                Text = status,
                Style = (Style)Application.Current.Resources["Celah.Text.Base"],
                FontSize = 10.5,
                LineHeight = 13,
                Foreground = B("TextSecondary"),
            });
        }
        Place(stack, 1);
        if (Compact) AddValue(status, "TextSecondary", small: true);
    }

    private void AddValue(string text, string brush, bool small = false, double width = double.NaN, double maxWidth = double.PositiveInfinity) =>
        Place(new TextBlock
        {
            Text = text,
            Style = (Style)Application.Current.Resources[small ? "Celah.Text.Caption" : "Celah.Text.Pill"],
            Foreground = B(brush),
            Width = width,
            MaxWidth = maxWidth,
            TextAlignment = TextAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        }, 2);

    private void AddChip(string text, string foreground, string background)
    {
        var chip = new Border
        {
            Height = 24,
            Padding = new Thickness(10, 0, 10, 0),
            CornerRadius = new CornerRadius(12),
            Background = B(background),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Child = new TextBlock
            {
                Text = text,
                Style = (Style)Application.Current.Resources["Celah.Text.Caption"],
                FontWeight = FontWeights.SemiBold,
                Foreground = B(foreground),
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Place(chip, 2);
    }

    /// <summary>Bar level 4 px. Bisu: bar tetap di level terakhir tapi redup.</summary>
    private void AddBar(double level, bool muted, double? animateFrom)
    {
        var track = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = B(muted ? "SurfaceHover" : "Stroke"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _fill = new Border
        {
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = B(muted ? "TextDisabled" : "TextPrimary"),
        };
        track.Child = _fill;
        Place(track, 1);

        // Lebar track baru diketahui setelah layout; perkirakan dulu supaya animasi langsung jalan.
        double target = Math.Clamp(level, 0, 1) * BarWidthEstimate;
        var fill = _fill;
        track.SizeChanged += (_, e) =>
        {
            double width = Math.Clamp(level, 0, 1) * e.NewSize.Width;
            fill.Animate(WidthProperty, Motion.To(width, Motion.Ms(160), Motion.Standard));
        };
        fill.Width = animateFrom ?? 0;
        fill.Animate(WidthProperty, Motion.To(target, Motion.Ms(animateFrom is null ? 240 : 160), Motion.Standard, animateFrom is null ? 200 : 0));
    }

    /// <summary>"EN → ID": kode lama redup, kode baru terang.</summary>
    private void AddLanguageSwitch(string from, string to)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var pill = (Style)Application.Current.Resources["Celah.Text.Pill"];
        stack.Children.Add(new TextBlock { Text = from, Style = pill, Foreground = B("TextTertiary") });
        var arrow = IconPath(ArrowIcon, B("TextTertiary"));
        arrow.StrokeThickness = 2.2;
        arrow.LayoutTransform = new ScaleTransform(0.5, 0.5);
        arrow.Margin = new Thickness(6, 0, 6, 0);
        stack.Children.Add(arrow);
        stack.Children.Add(new TextBlock { Text = to, Style = pill });
        Place(stack, 1);
    }

    private void AddBattery(int percent, string brush)
    {
        var color = B(brush);
        var canvas = new Canvas { Width = 22, Height = 18, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        var body = new Rectangle { Width = 17, Height = 10, RadiusX = 2.2, RadiusY = 2.2, Stroke = color, StrokeThickness = 1.6 };
        Canvas.SetLeft(body, 1);
        Canvas.SetTop(body, 4);
        double inner = Math.Max(1.6, 12.6 * Math.Clamp(percent, 0, 100) / 100.0);
        var level = new Rectangle { Width = inner, Height = 5.4, RadiusX = 0.8, RadiusY = 0.8, Fill = color };
        Canvas.SetLeft(level, 3.2);
        Canvas.SetTop(level, 6.3);
        var tip = new Rectangle { Width = 1.8, Height = 4, RadiusX = 0.9, RadiusY = 0.9, Fill = color };
        Canvas.SetLeft(tip, 19.2);
        Canvas.SetTop(tip, 7);
        canvas.Children.Add(body);
        canvas.Children.Add(level);
        canvas.Children.Add(tip);
        Place(canvas, 0);
    }

    /// <summary>Charger dicolok: Kedip tersenyum (^ ^) menggantikan ikon.</summary>
    private void AddHappyEyes()
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Width = 22, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        for (int i = 0; i < 2; i++)
        {
            stack.Children.Add(new Path
            {
                Data = HappyEye,
                Stroke = B("Accent"),
                StrokeThickness = 3.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Width = 16,
                Height = 12,
                LayoutTransform = new ScaleTransform(0.5, 0.5),
                Margin = new Thickness(i == 0 ? 1 : 4, 0, 0, 0),
            });
        }
        Place(stack, 0);
    }

    private void AddBoltValue(int percent)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        stack.Children.Add(new Path { Data = BoltIcon, Fill = B("Accent"), Width = 24, Height = 24, LayoutTransform = new ScaleTransform(0.5, 0.5), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = $"{percent}%", Style = (Style)Application.Current.Resources["Celah.Text.Pill"], Foreground = B("Accent"), VerticalAlignment = VerticalAlignment.Center });
        Place(stack, 2);
    }
}
