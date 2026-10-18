using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Celah.App.Animation;
using Celah.Core.Notch;
using Celah.Platform.Display;

namespace Celah.App.Notch;

/// <summary>
/// Jendela notch berukuran tetap 640 × 320 (transparan). Yang dianimasikan hanya ukuran Border
/// <c>Body</c> dan opacity/transform lapisan isi, sesuai Motion.dc.html.
/// </summary>
public partial class NotchWindow : Window
{
    private static readonly Size CollapsedSize = new(200, 32);
    private static readonly Size PeekSize = new(320, 44);
    private static readonly Size ExpandedSize = new(560, 260);

    private static readonly CornerRadius CollapsedRadius = new(0, 0, 16, 16);
    private static readonly CornerRadius PeekRadius = new(0, 0, 22, 22);
    private static readonly CornerRadius ExpandedRadius = new(0, 0, 28, 28);

    private readonly DispatcherTimer _clockTimer = new(DispatcherPriority.Background);
    private NotchState _visualState = NotchState.Collapsed;

    public NotchWindow()
    {
        InitializeComponent();
        _clockTimer.Tick += (_, _) => UpdateClock();
    }

    public IntPtr Handle { get; private set; }

    /// <summary>Area yang benar-benar terlihat (bahu + badan); dipakai untuk deteksi hover.</summary>
    public FrameworkElement HoverTarget => ShapeHost;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Handle = new WindowInteropHelper(this).Handle;
        NotchWindowInterop.ApplyOverlayStyles(Handle);
        Reposition();
        Kedip.StartIdle();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(Reposition, DispatcherPriority.Loaded);
    }

    /// <summary>Tengah atas monitor utama (multi-monitor: keputusan v1 = monitor utama).</summary>
    public void Reposition()
    {
        if (Handle == IntPtr.Zero) return;
        NotchWindowInterop.PlaceTopCenter(Handle, NotchWindowInterop.GetPrimaryMonitor());
    }

    public void EnsureTopmost()
    {
        if (Handle != IntPtr.Zero && IsVisible)
            NotchWindowInterop.EnsureTopmost(Handle);
    }

    public void TransitionTo(NotchState from, NotchState to)
    {
        _visualState = to;
        switch (to)
        {
            case NotchState.Expanded: ToExpanded(); break;
            case NotchState.Collapsed: ToCollapsed(from); break;
            case NotchState.Peek: ToPeek(from); break;
            case NotchState.Hidden: ToHidden(); break;
        }
    }

    // ── Transisi ────────────────────────────────────────────────────────────

    private void ToExpanded()
    {
        Kedip.StopIdle();
        FadeOut(CollapsedLayer, Motion.Ms(100));
        ScaleCollapsed(0.96, Motion.Ms(100), Motion.In);
        FadeOut(PeekLayer, Motion.Ms(100));

        AnimateShape(ExpandedSize, ExpandedRadius, Motion.ShapeOpen, Motion.Spring, widthDelay: 0, heightDelay: 50);

        UpdateClock();
        ExpandedLayer.Visibility = Visibility.Visible;
        FadeIn(ExpandedLayer, Motion.Base, delayMs: 220);
        Slide(TabBarShift, TranslateTransform.YProperty, from: -6, Motion.Base, delayMs: 220);
        Slide(ModuleShift, TranslateTransform.YProperty, from: 8, Motion.Ms(300), delayMs: 260);
    }

    private void ToCollapsed(NotchState from)
    {
        _clockTimer.Stop();
        ShowWindowIfHidden();

        if (from == NotchState.Hidden)
        {
            // Turun kembali dari atas layar.
            SetShapeImmediately(CollapsedSize, CollapsedRadius);
            ShowLayerImmediately(CollapsedLayer);
            HideShift.Animate(TranslateTransform.YProperty,
                Motion.To(0, Reduced(Motion.Ms(380)), Motion.SpringSoft));
            ShapeHost.Animate(OpacityProperty, Motion.To(1, Motion.Ms(120), Motion.Out));
        }
        else if (from == NotchState.Expanded)
        {
            FadeOut(ExpandedLayer, Motion.Fast);
            Slide(TabBarShift, TranslateTransform.YProperty, from: null, Motion.Fast, to: -4, ease: Motion.In);
            AnimateShape(CollapsedSize, CollapsedRadius, Motion.ShapeClose, Motion.Standard, widthDelay: 110, heightDelay: 80);
            FadeIn(CollapsedLayer, Motion.Base, delayMs: 380);
            ScaleCollapsed(1, Motion.Base, Motion.Out, delayMs: 380);
            Kedip.Blink(delayMs: 520);
        }
        else
        {
            FadeOut(PeekLayer, Motion.Fast);
            AnimateShape(CollapsedSize, CollapsedRadius, Motion.Ms(280), Motion.Standard, widthDelay: 60, heightDelay: 60);
            FadeIn(CollapsedLayer, Motion.Base, delayMs: 180);
            ScaleCollapsed(1, Motion.Base, Motion.Out, delayMs: 180);
        }

        Kedip.StartIdle();
        Kedip.BreatheOnce();
    }

    private void ToPeek(NotchState from)
    {
        ShowWindowIfHidden();
        if (from == NotchState.Hidden)
        {
            // Peek penting saat fullscreen: turunkan notch langsung ke bentuk peek.
            HideShift.Animate(TranslateTransform.YProperty, Motion.To(0, Reduced(Motion.Ms(380)), Motion.SpringSoft));
            ShapeHost.Animate(OpacityProperty, Motion.To(1, Motion.Ms(120), Motion.Out));
        }

        FadeOut(CollapsedLayer, Motion.Ms(100));
        AnimateShape(PeekSize, PeekRadius, Motion.PeekIn, Motion.SpringSoft, widthDelay: 40, heightDelay: 40);

        PeekLayer.Visibility = Visibility.Visible;
        FadeIn(PeekLayer, Motion.Base, delayMs: 160);
        Slide(PeekShift, TranslateTransform.XProperty, from: -6, Motion.Base, delayMs: 160);

        // Contoh isi bar level (nanti diisi data volume sungguhan).
        int value = Random.Shared.Next(10, 100);
        PeekValue.Text = value.ToString();
        PeekFill.Animate(WidthProperty, Motion.To(value / 100.0 * 232, Motion.Ms(240), Motion.Standard, delayMs: 200));
    }

    private void ToHidden()
    {
        _clockTimer.Stop();
        Kedip.StopIdle();

        double distance = Body.ActualHeight + 12;
        var up = Motion.To(-distance, Reduced(Motion.Ms(260)), Motion.In);
        up.Completed += (_, _) =>
        {
            // Sembunyikan jendela sungguhan supaya tidak menumpuk di atas game/video fullscreen.
            if (_visualState == NotchState.Hidden) Hide();
        };
        HideShift.Animate(TranslateTransform.YProperty, up);
        ShapeHost.Animate(OpacityProperty, new DoubleAnimation(0, Reduced(Motion.Ms(80))) { BeginTime = Motion.Ms(Motion.Reduced ? 0 : 180) });
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    private void ShowWindowIfHidden()
    {
        if (IsVisible) return;
        Show();
        EnsureTopmost();
    }

    private static TimeSpan Reduced(TimeSpan normal) => Motion.Reduced ? TimeSpan.Zero : normal;

    private void AnimateShape(Size size, CornerRadius radius, TimeSpan duration, IEasingFunction ease, double widthDelay, double heightDelay)
    {
        if (Motion.Reduced)
        {
            SetShapeImmediately(size, radius);
            return;
        }
        Body.Animate(WidthProperty, Motion.To(size.Width, duration, ease, widthDelay));
        Body.Animate(HeightProperty, Motion.To(size.Height, duration, ease, heightDelay));
        var r = new CornerRadiusAnimation { To = radius, Duration = duration, EasingFunction = ease, BeginTime = Motion.Ms(widthDelay) };
        Timeline.SetDesiredFrameRate(r, 60);
        Body.Animate(System.Windows.Controls.Border.CornerRadiusProperty, r);
    }

    private void SetShapeImmediately(Size size, CornerRadius radius)
    {
        Body.BeginAnimation(WidthProperty, null);
        Body.BeginAnimation(HeightProperty, null);
        Body.BeginAnimation(System.Windows.Controls.Border.CornerRadiusProperty, null);
        Body.Width = size.Width;
        Body.Height = size.Height;
        Body.CornerRadius = radius;
    }

    private void ShowLayerImmediately(UIElement layer)
    {
        foreach (var other in new UIElement[] { CollapsedLayer, PeekLayer, ExpandedLayer })
        {
            other.BeginAnimation(OpacityProperty, null);
            other.Opacity = other == layer ? 1 : 0;
            other.Visibility = other == layer ? Visibility.Visible : Visibility.Collapsed;
        }
        CollapsedScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        CollapsedScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }

    private static void FadeIn(UIElement layer, TimeSpan duration, double delayMs = 0)
    {
        layer.Visibility = Visibility.Visible;
        if (Motion.Reduced) { duration = Motion.ReducedCrossfade; delayMs = 0; }
        layer.Animate(OpacityProperty, Motion.To(1, duration, Motion.Out, delayMs));
    }

    private void FadeOut(UIElement layer, TimeSpan duration)
    {
        if (layer.Visibility != Visibility.Visible) return;
        if (Motion.Reduced) duration = Motion.ReducedCrossfade;
        var a = Motion.To(0, duration, Motion.In);
        a.Completed += (_, _) =>
        {
            // Lapisan yang tak terlihat di-collapse supaya tidak ikut layout/render.
            if (layer.Opacity == 0) layer.Visibility = Visibility.Collapsed;
        };
        layer.Animate(OpacityProperty, a);
    }

    private void ScaleCollapsed(double to, TimeSpan duration, IEasingFunction ease, double delayMs = 0)
    {
        if (Motion.Reduced) to = 1;
        CollapsedScale.Animate(ScaleTransform.ScaleXProperty, Motion.To(to, duration, ease, delayMs));
        CollapsedScale.Animate(ScaleTransform.ScaleYProperty, Motion.To(to, duration, ease, delayMs));
    }

    private static void Slide(TranslateTransform target, DependencyProperty property, double? from, TimeSpan duration,
        double delayMs = 0, double to = 0, IEasingFunction? ease = null)
    {
        if (Motion.Reduced)
        {
            target.BeginAnimation(property, null);
            target.SetValue(property, 0.0);
            return;
        }
        var a = Motion.To(to, duration, ease ?? Motion.Out, delayMs);
        if (from is { } f) a.From = f;
        target.Animate(property, a);
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        Clock.Text = now.ToString("HH:mm");
        _clockTimer.Interval = TimeSpan.FromSeconds(60 - now.Second) + TimeSpan.FromMilliseconds(50);
        if (_visualState == NotchState.Expanded) _clockTimer.Start();
    }
}
