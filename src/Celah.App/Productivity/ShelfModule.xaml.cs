using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Celah.App.Animation;
using Celah.Core.Productivity;

namespace Celah.App.Productivity;

public partial class ShelfModule : UserControl
{
    private static readonly HashSet<string> ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff"];
    private readonly Dictionary<string, ImageSource?> _thumbs = [];
    private Shelf? _shelf;
    private bool _dragOver;

    public ShelfModule()
    {
        InitializeComponent();
    }

    /// <summary>Sedang menyeret file keluar dari rak atau membuka dialog: panel harus tetap terbuka.</summary>
    public event Action<bool>? BusyChanged;

    public void Attach(Shelf shelf)
    {
        _shelf = shelf;
        shelf.Changed += Rebuild;
        Rebuild();
    }

    public void Rebuild()
    {
        if (_shelf is null) return;
        var items = _shelf.Items;
        bool empty = items.Count == 0;
        DropZone.Visibility = empty || _dragOver ? Visibility.Visible : Visibility.Collapsed;
        FilledView.Visibility = empty || _dragOver ? Visibility.Collapsed : Visibility.Visible;
        SummaryText.Text = $"{items.Count} file · {Shelf.FormatSize(_shelf.TotalBytes)}";

        Cards.Children.Clear();
        foreach (var item in items) Cards.Children.Add(BuildCard(item));
    }

    /// <summary>
    /// File sedang diseret di atas notch. Zona: garis accent 2 px + latar accent-muted, panah naik-turun
    /// 4 px tiap 800 ms. Dilepas: zona "menelan" file (scale 1 → 0.98 → 1).
    /// </summary>
    public void SetDragOver(bool over, int count = 0, long bytes = 0, bool dropped = false)
    {
        _dragOver = over;
        if (over)
        {
            ZoneBorder.Stroke = B("Accent");
            ZoneBorder.StrokeThickness = 2;
            ZoneBorder.StrokeDashArray = [3, 2];
            ZoneFill.Fill = B("AccentMuted");
            ZoneIcon.Stroke = B("Accent");
            ZoneTitle.Text = "Lepaskan untuk menyimpan";
            ZoneTitle.Foreground = B("Accent");
            ZoneDetail.Text = bytes > 0 ? $"{count} file · {Shelf.FormatSize(bytes)}" : $"{count} file";
            ZoneDetail.Foreground = B("TextSecondary");
            if (!Motion.Reduced)
            {
                var bob = new DoubleAnimation(0, 4, TimeSpan.FromMilliseconds(400)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = Motion.Sine };
                Timeline.SetDesiredFrameRate(bob, 30);
                ZoneIconBob.BeginAnimation(TranslateTransform.YProperty, bob);
            }
        }
        else
        {
            ZoneBorder.Stroke = B("StrokeStrong");
            ZoneBorder.StrokeThickness = 1.5;
            ZoneBorder.StrokeDashArray = [4, 2.67];
            ZoneFill.Fill = Brushes.Transparent;
            ZoneIcon.Stroke = B("TextSecondary");
            ZoneTitle.Text = "Seret file ke sini";
            ZoneTitle.Foreground = B("TextPrimary");
            ZoneDetail.Text = "Simpan sementara, lalu seret keluar ke mana saja";
            ZoneDetail.Foreground = B("TextTertiary");
            ZoneIconBob.BeginAnimation(TranslateTransform.YProperty, null);
            if (dropped && !Motion.Reduced)
            {
                var swallow = new DoubleAnimationUsingKeyFrames { Duration = Motion.Ms(260) };
                swallow.KeyFrames.Add(new EasingDoubleKeyFrame(0.98, KeyTime.FromPercent(0.35)) { EasingFunction = Motion.In });
                swallow.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1)) { EasingFunction = Motion.Spring });
                ZoneScale.BeginAnimation(ScaleTransform.ScaleXProperty, swallow);
                ZoneScale.BeginAnimation(ScaleTransform.ScaleYProperty, swallow);
            }
        }
        Rebuild();
    }

    // ── Kartu ───────────────────────────────────────────────────────────────

    private FrameworkElement BuildCard(ShelfItem item)
    {
        var card = new Grid { Width = 88, Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand, ToolTip = item.Path };
        var bg = new Border { CornerRadius = new CornerRadius(12), Background = Brushes.Transparent };
        card.Children.Add(bg);

        var stack = new StackPanel { Margin = new Thickness(4, 6, 4, 6) };
        var thumb = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(10),
            Background = B("SurfaceRaised"),
            ClipToBounds = true,
        };
        var image = Thumbnail(item.Path);
        if (image is not null && ImageExtensions.Contains(System.IO.Path.GetExtension(item.Path).ToLowerInvariant()))
        {
            thumb.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        }
        else if (image is not null)
        {
            thumb.Child = new Image { Source = image, Width = 32, Height = 32 };
        }
        else
        {
            thumb.Child = new System.Windows.Shapes.Path
            {
                Style = (Style)FindResource("Celah.IconPath"),
                Data = (Geometry)FindResource(Directory.Exists(item.Path) ? "Celah.Icon.Folder" : "Celah.Icon.File"),
                Stroke = B("TextSecondary"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        stack.Children.Add(thumb);
        stack.Children.Add(new TextBlock
        {
            Text = System.IO.Path.GetFileName(item.Path.TrimEnd('\\')),
            Style = (Style)FindResource("Celah.Text.Caption"),
            Foreground = B("TextPrimary"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 28,
            Margin = new Thickness(0, 6, 0, 0),
        });
        card.Children.Add(stack);

        var remove = new Button
        {
            Style = (Style)FindResource("Celah.Button.Icon"),
            Tag = FindResource("Celah.Icon.Close"),
            Width = 20,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 6, 0),
            Opacity = 0,
            ToolTip = "Hapus dari rak",
            Background = B("SurfacePressed"),
        };
        remove.Click += (_, _) => _shelf?.Remove(item.Id);
        card.Children.Add(remove);

        card.MouseEnter += (_, _) =>
        {
            bg.Background = B("SurfaceRaised");
            remove.Animate(OpacityProperty, Motion.To(1, Motion.Fast, Motion.Out));
        };
        card.MouseLeave += (_, _) =>
        {
            bg.Background = Brushes.Transparent;
            remove.Animate(OpacityProperty, Motion.To(0, Motion.Fast, Motion.Out));
        };

        // Klik dua kali membuka; seret keluar memindahkan/menyalin ke aplikasi tujuan.
        Point? pressedAt = null;
        card.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                Open(item.Path);
                e.Handled = true;
                return;
            }
            pressedAt = e.GetPosition(card);
        };
        card.PreviewMouseLeftButtonUp += (_, _) => pressedAt = null;
        card.PreviewMouseMove += (_, e) =>
        {
            if (pressedAt is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
            var delta = e.GetPosition(card) - start;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            pressedAt = null;
            DragOut(card, item);
        };
        return card;
    }

    private void DragOut(FrameworkElement source, ShelfItem item)
    {
        BusyChanged?.Invoke(true);
        try
        {
            var data = new DataObject(DataFormats.FileDrop, new[] { item.Path });
            DragDrop.DoDragDrop(source, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
        }
        finally
        {
            BusyChanged?.Invoke(false);
            // File yang dipindahkan aplikasi tujuan hilang dari tempat asalnya: buang dari rak.
            _shelf?.RemoveMissing();
        }
    }

    private static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    /// <summary>Gambar memakai pratinjau asli (112 px); file lain memakai ikon tipe dari Windows.</summary>
    private ImageSource? Thumbnail(string path)
    {
        if (_thumbs.TryGetValue(path, out var cached)) return cached;
        ImageSource? result = null;
        try
        {
            if (ImageExtensions.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()) && File.Exists(path))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 112;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                result = bmp;
            }
            else if (File.Exists(path))
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon is not null)
                {
                    var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    result = src;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            result = null;
        }
        _thumbs[path] = result;
        return result;
    }

    private void OnZoneClick(object sender, MouseButtonEventArgs e)
    {
        if (_shelf is null || _dragOver) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "Simpan file di rak" };
        BusyChanged?.Invoke(true);
        try
        {
            if (dialog.ShowDialog(Window.GetWindow(this)) == true) _shelf.Add(dialog.FileNames);
        }
        finally
        {
            BusyChanged?.Invoke(false);
        }
    }

    private void OnClear(object sender, RoutedEventArgs e) => _shelf?.Clear();

    /// <summary>Roda mouse menggulir kartu ke samping.</summary>
    private void OnCardWheel(object sender, MouseWheelEventArgs e)
    {
        CardScroll.ScrollToHorizontalOffset(CardScroll.HorizontalOffset - e.Delta / 2.0);
        e.Handled = true;
    }

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Celah.Brush.{token}"];
}
