using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Celah.App.Animation;

namespace Celah.App.Controls;

/// <summary>
/// Slider posisi lagu (Components.dc.html): track 4 px, menebal ke 6 px dan memunculkan thumb
/// 12 px saat di-hover. Hit area 20 px. Tanpa dukungan seek: fill tertiary, tanpa thumb.
/// </summary>
public sealed class SeekBar : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(SeekBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty IsSeekableProperty = DependencyProperty.Register(
        nameof(IsSeekable), typeof(bool), typeof(SeekBar),
        new FrameworkPropertyMetadata(true, (d, _) => ((SeekBar)d).UpdateVisuals()));

    private readonly Grid _root = new() { Background = Brushes.Transparent };
    private readonly Border _track = new() { Height = 4, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _fill = new() { Height = 4, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private readonly Ellipse _thumb = new() { Width = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(0.5, 0.5) };
    private readonly ScaleTransform _thumbScale = new(0, 0);

    private bool _dragging;
    private double _dragFraction;
    private double? _pendingFraction;
    private DateTime _pendingUntil;

    public SeekBar()
    {
        Height = 20;
        Cursor = Cursors.Hand;
        _thumb.RenderTransform = _thumbScale;
        _root.Children.Add(_track);
        _root.Children.Add(_fill);
        _root.Children.Add(_thumb);
        AddVisualChild(_root);
        Loaded += (_, _) => UpdateVisuals();
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool IsSeekable
    {
        get => (bool)GetValue(IsSeekableProperty);
        set => SetValue(IsSeekableProperty, value);
    }

    /// <summary>Pengguna melepas tombol mouse di posisi tertentu (0–1).</summary>
    public event Action<double>? SeekRequested;

    /// <summary>Scroll di atas slider: +1 maju, −1 mundur.</summary>
    public event Action<int>? Nudged;

    /// <summary>true saat mulai digeser, false saat selesai (dipakai untuk menahan panel tetap terbuka).</summary>
    public event Action<bool>? DraggingChanged;

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _root;

    protected override Size MeasureOverride(Size availableSize)
    {
        _root.Measure(availableSize);
        return new Size(0, 20);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_pendingFraction is { } pending &&
            (Math.Abs(Progress - pending) < 0.01 || DateTime.UtcNow > _pendingUntil))
            _pendingFraction = null;

        double fraction = Math.Clamp(_dragging ? _dragFraction : _pendingFraction ?? Progress, 0, 1);
        double width = finalSize.Width;
        _fill.Width = width * fraction;
        _thumb.Margin = new Thickness(width * fraction - 6, 0, 0, 0);
        _root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private void UpdateVisuals()
    {
        var res = Application.Current.Resources;
        bool seekable = IsSeekable;
        _track.Background = (Brush)res[seekable ? "Celah.Brush.Stroke" : "Celah.Brush.SurfaceHover"];
        _fill.Background = (Brush)res[seekable ? "Celah.Brush.TextPrimary" : "Celah.Brush.TextTertiary"];
        _thumb.Fill = (Brush)res["Celah.Brush.TextPrimary"];
        Cursor = seekable ? Cursors.Hand : Cursors.Arrow;
        if (!seekable) SetHover(false);
    }

    private void SetHover(bool on)
    {
        on &= IsSeekable;
        double thickness = on ? 6 : 4;
        _track.Animate(HeightProperty, Motion.To(thickness, Motion.Fast, Motion.Out));
        _fill.Animate(HeightProperty, Motion.To(thickness, Motion.Fast, Motion.Out));
        _thumbScale.Animate(ScaleTransform.ScaleXProperty, Motion.To(on ? 1 : 0, Motion.Fast, Motion.Out));
        _thumbScale.Animate(ScaleTransform.ScaleYProperty, Motion.To(on ? 1 : 0, Motion.Fast, Motion.Out));
    }

    protected override void OnMouseEnter(MouseEventArgs e) => SetHover(true);

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (!_dragging) SetHover(false);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsSeekable) return;
        _dragging = true;
        _dragFraction = FractionAt(e);
        CaptureMouse();
        DraggingChanged?.Invoke(true);
        InvalidateArrange();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_dragging) return;
        _dragFraction = FractionAt(e);
        InvalidateArrange();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragFraction = FractionAt(e);
        // Tampilkan posisi tujuan sampai laporan posisi baru dari pemutar tiba (maks. 1,5 dtk),
        // supaya bar tidak sempat melompat kembali ke posisi lama.
        _pendingFraction = _dragFraction;
        _pendingUntil = DateTime.UtcNow.AddSeconds(1.5);
        EndDrag();
        SeekRequested?.Invoke(_dragFraction);
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_dragging) EndDrag();
    }

    private void EndDrag()
    {
        _dragging = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (!IsMouseOver) SetHover(false);
        DraggingChanged?.Invoke(false);
        InvalidateArrange();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (!IsSeekable) return;
        Nudged?.Invoke(Math.Sign(e.Delta));
        e.Handled = true;
    }

    private double FractionAt(MouseEventArgs e) =>
        ActualWidth > 0 ? Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1) : 0;
}
