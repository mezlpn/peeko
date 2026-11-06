using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Peeko.Platform.Display;

namespace Peeko.App.Sistem;

/// <summary>
/// Pilihan zona saat jendela diseret ke notch: delapan layar mini, zona di bawah kursor menyala.
/// Lepas tombol mouse di atasnya untuk menata jendela.
/// </summary>
public sealed class SnapModule : StackPanel
{
    public const double ContentWidth = 440;
    public const double ContentHeight = 74;

    private readonly List<(Border Tile, Rectangle Area, TextBlock Label, SnapZone Zone)> _tiles = [];

    public SnapModule()
    {
        Width = ContentWidth;
        Children.Add(new TextBlock
        {
            Text = "Lepas di salah satu zona untuk menata jendela",
            Style = (Style)Application.Current.Resources["Peeko.Text.Caption"],
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
        });
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var zone in WindowSnapper.Zones)
        {
            var canvas = new Canvas { Width = 44, Height = 28 };
            var area = new Rectangle
            {
                Width = Math.Max(4, 44 * zone.Width - 3),
                Height = 28 * zone.Height - 3,
                RadiusX = 3,
                RadiusY = 3,
                Fill = B("SurfacePressed"),
            };
            Canvas.SetLeft(area, 44 * zone.X + 1.5);
            Canvas.SetTop(area, 28 * zone.Y + 1.5);
            canvas.Children.Add(area);
            var tile = new Border
            {
                Width = 48,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1.5),
                BorderBrush = B("StrokeStrong"),
                Padding = new Thickness(0.5),
                Child = canvas,
            };
            var label = new TextBlock
            {
                Text = zone.Name,
                Style = (Style)Application.Current.Resources["Peeko.Text.Caption"],
                FontSize = 9.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
            };
            var cell = new StackPanel { Width = 54 };
            cell.Children.Add(tile);
            cell.Children.Add(label);
            row.Children.Add(cell);
            _tiles.Add((tile, area, label, zone));
        }
        Children.Add(row);
    }

    public SnapZone? Hovered { get; private set; }

    /// <summary>Kursor (piksel layar) bergerak selama seretan: nyalakan zona di bawahnya.</summary>
    public void Hover(int screenX, int screenY)
    {
        SnapZone? hit = null;
        foreach (var (tile, _, _, zone) in _tiles)
        {
            if (PresentationSource.FromVisual(tile) is null) continue;
            var p = tile.PointFromScreen(new Point(screenX, screenY));
            // Area tangkap sedikit lebih besar dari ubinnya supaya mudah dikenai.
            if (p.X >= -3 && p.X <= tile.ActualWidth + 3 && p.Y >= -12 && p.Y <= tile.ActualHeight + 22) hit = zone;
        }
        if (hit == Hovered) return;
        Hovered = hit;
        foreach (var (tile, area, label, zone) in _tiles)
        {
            bool on = zone == hit;
            tile.BorderBrush = B(on ? "Accent" : "StrokeStrong");
            area.Fill = B(on ? "Accent" : "SurfacePressed");
            label.Foreground = B(on ? "TextPrimary" : "TextTertiary");
        }
    }

    public void Reset() => Hover(int.MinValue / 2, int.MinValue / 2);

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
