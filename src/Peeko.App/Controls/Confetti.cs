using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Peeko.App.Controls;

/// <summary>
/// Semburan konfeti singkat (±1,6 detik) di atas sebuah Canvas transparan. Dipakai saat semua tugas
/// selesai. Fisika sederhana per frame: kecepatan awal ke atas, gravitasi, hambatan udara, berputar,
/// lalu memudar. Canvas dibersihkan sendiri setelah selesai.
/// </summary>
public static class Confetti
{
    private static readonly Color[] Palette =
    [
        Color.FromRgb(0xC6, 0xF3, 0x6B), // accent
        Color.FromRgb(0x6C, 0xC8, 0xF2), // water
        Color.FromRgb(0xF2, 0xB8, 0x66), // rest
        Color.FromRgb(0xFF, 0x6B, 0x5B), // danger
        Color.FromRgb(0xF5, 0xF5, 0xF7),
    ];

    private sealed class Piece
    {
        public required Shape Shape;
        public required RotateTransform Rotate;
        public double X, Y, Vx, Vy, Spin;
    }

    public static void Burst(Canvas layer, Point origin, int count = 46)
    {
        var rng = new Random();
        var pieces = new List<Piece>(count);
        for (int i = 0; i < count; i++)
        {
            var color = Palette[rng.Next(Palette.Length)];
            Shape shape = rng.Next(3) == 0
                ? new Ellipse { Width = 5, Height = 5 }
                : new Rectangle { Width = 4 + rng.NextDouble() * 3, Height = 7 + rng.NextDouble() * 4, RadiusX = 1, RadiusY = 1 };
            shape.Fill = new SolidColorBrush(color);
            var rotate = new RotateTransform(rng.NextDouble() * 360);
            shape.RenderTransform = rotate;
            shape.RenderTransformOrigin = new Point(0.5, 0.5);
            shape.IsHitTestVisible = false;

            // Kipas ke atas, -150°…-30°.
            double angle = (-150 + rng.NextDouble() * 120) * Math.PI / 180;
            double speed = 260 + rng.NextDouble() * 320;
            var p = new Piece
            {
                Shape = shape,
                Rotate = rotate,
                X = origin.X,
                Y = origin.Y,
                Vx = Math.Cos(angle) * speed,
                Vy = Math.Sin(angle) * speed,
                Spin = (rng.NextDouble() - 0.5) * 900,
            };
            Canvas.SetLeft(shape, p.X);
            Canvas.SetTop(shape, p.Y);
            layer.Children.Add(shape);
            pieces.Add(p);
        }

        const double duration = 1.6, gravity = 900, drag = 1.8;
        var start = DateTime.UtcNow;
        var last = start;
        EventHandler? tick = null;
        tick = (_, _) =>
        {
            var now = DateTime.UtcNow;
            double dt = Math.Min((now - last).TotalSeconds, 0.05);
            last = now;
            double t = (now - start).TotalSeconds;
            double fade = t < duration * 0.6 ? 1 : Math.Max(0, 1 - (t - duration * 0.6) / (duration * 0.4));
            foreach (var p in pieces)
            {
                p.Vx -= p.Vx * drag * dt;
                p.Vy += gravity * dt - p.Vy * drag * dt;
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                p.Rotate.Angle += p.Spin * dt;
                Canvas.SetLeft(p.Shape, p.X);
                Canvas.SetTop(p.Shape, p.Y);
                p.Shape.Opacity = fade;
            }
            if (t >= duration)
            {
                CompositionTarget.Rendering -= tick;
                foreach (var p in pieces) layer.Children.Remove(p.Shape);
            }
        };
        CompositionTarget.Rendering += tick;
    }
}
