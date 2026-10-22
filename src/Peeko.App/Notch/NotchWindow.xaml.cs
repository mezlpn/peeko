using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Peeko.App.Animation;
using Peeko.App.Lyrics;
using Peeko.App.Music;
using Peeko.App.Productivity;
using Peeko.Core.Notch;
using Peeko.Core.Peeks;
using Peeko.Platform.Display;

namespace Peeko.App.Notch;

/// <summary>
/// Jendela notch berukuran tetap 640 × 320 (transparan). Yang dianimasikan hanya ukuran Border
/// <c>Body</c> dan opacity/transform lapisan isi, sesuai Motion.dc.html (dengan revisi kecepatan
/// hover dan mode tipis, lihat ARSITEKTUR §10).
/// </summary>
public partial class NotchWindow : Window
{
    private static readonly Size SlimSize = new(160, 4);
    private static readonly Size IdleSize = new(200, 32);
    // Revisi #12: ukuran pil musik sama dengan pil idle; isinya yang dirapatkan di tengah.
    private static readonly Size MusicSize = IdleSize;
    private static readonly Size WaterSize = new(260, 32);
    private static readonly Size PeekSize = new(320, 44);

    // Respons hover instan: pil sedikit menggembung sebelum panel terbuka.
    private static readonly Size SlimHoverSize = new(184, 6);
    private static readonly Size HoverGrow = new(12, 2);

    private static readonly CornerRadius SlimRadius = new(0, 0, 2, 2);
    private static readonly CornerRadius CollapsedRadius = new(0, 0, 16, 16);
    private static readonly CornerRadius PeekRadius = new(0, 0, 22, 22);
    private static readonly CornerRadius ExpandedRadius = new(0, 0, 28, 28);

    private readonly DispatcherTimer _clockTimer = new(DispatcherPriority.Background);
    private readonly DispatcherTimer _bannerTimer = new();
    private Size _peekSize = PeekSize;
    private NotchState _visualState = NotchState.Collapsed;
    private CollapsedActivity _activity = CollapsedActivity.Idle;
    private int _moduleIndex;

    // Bentuk notch digerakkan pegas, satu per sifat (cara Bloom): lebar, tinggi, radius, bahu, dan
    // posisi sembunyi masing-masing punya kekakuan sendiri dan bisa dibalik di tengah gerak.
    private readonly SpringValue _width;
    private readonly SpringValue _height;
    private readonly SpringValue _radius;
    private readonly SpringValue _shoulders;
    private readonly SpringValue _hideY;

    public NotchWindow(MusicViewModel music, LyricsViewModel lyrics)
    {
        InitializeComponent();
        _width = new SpringValue(IdleSize.Width, SpringConfig.Width, v => Body.Width = Math.Max(0, v));
        _height = new SpringValue(IdleSize.Height, SpringConfig.Height, v => Body.Height = Math.Max(0, v));
        _radius = new SpringValue(CollapsedRadius.BottomLeft, SpringConfig.Radius, v =>
        {
            double r = Math.Max(0, v);
            Body.CornerRadius = new CornerRadius(0, 0, r, r);
        });
        _shoulders = new SpringValue(1, SpringConfig.Default, v =>
        {
            double s = Math.Max(0, v);
            LeftShoulderScale.ScaleX = LeftShoulderScale.ScaleY = s;
            RightShoulderScale.ScaleX = RightShoulderScale.ScaleY = s;
        });
        _hideY = new SpringValue(0, SpringConfig.Offset, v => HideShift.Y = v);
        _clockTimer.Tick += (_, _) => UpdateClock();
        _bannerTimer.Tick += (_, _) => HideBanner();
        MusicPillView.DataContext = music;
        MusicModuleView.DataContext = music;
        LyricsModuleView.DataContext = lyrics;
        WatchContentSize();
        BuildDots();
    }

    /// <summary>
    /// Ukuran pil per isi: semuanya 200 × 32 (revisi #12), kecuali pengingat minum yang butuh ruang
    /// untuk teks dan chip "+1 gelas".
    /// </summary>
    private Size CollapsedSize => _activity switch
    {
        CollapsedActivity.Water or CollapsedActivity.Update => WaterSize,
        CollapsedActivity.Music => MusicSize,
        _ => IdleSize,
    };

    public MusicPill MusicPill => MusicPillView;

    public MusicModule MusicModule => MusicModuleView;

    public LyricsModule LyricsModule => LyricsModuleView;

    public FocusPill FocusPill => FocusPillView;

    public WaterPill WaterPill => WaterPillView;

    public Updates.UpdatePill UpdatePill => UpdatePillView;

    public System.Windows.Controls.Button UpdateButton => UpdateChip;

    private bool _updateHighlight;

    /// <summary>
    /// Ada update: tombol di sebelah jam muncul, dan garis tipis berwarna limau supaya tetap
    /// terlihat walau notch sedang berupa garis (mis. di atas tab Chrome).
    /// </summary>
    public void SetUpdateHighlight(string? version)
    {
        _updateHighlight = version is not null;
        UpdateChip.Visibility = _updateHighlight ? Visibility.Visible : Visibility.Collapsed;
        UpdateChip.Content = $"Update {version}";
        UpdateBodyTint();
    }

    private void UpdateBodyTint() =>
        Body.Background = (Brush)FindResource(_updateHighlight && _visualState == NotchState.Slim
            ? "Peeko.Brush.Accent"
            : "Peeko.Brush.NotchBg");

    public FocusModule FocusModule => FocusModuleView;

    public WaterModule WaterModule => WaterModuleView;

    public TodoModule TodoModule => TodoModuleView;

    public ShelfModule ShelfModule => ShelfModuleView;

    public Control.ControlModule ControlModule => ControlModuleView;

    public Today.TodayModule TodayModule => TodayModuleView;

    public FrameworkElement DropTarget => ShapeHost;

    /// <summary>
    /// Input teks (to-do) dibuka: jendela notch sementara boleh aktif supaya bisa menerima ketikan.
    /// Normalnya WS_EX_NOACTIVATE agar tidak mencuri fokus dari aplikasi lain.
    /// </summary>
    public void SetTextInput(bool active)
    {
        if (Handle == IntPtr.Zero) return;
        NotchWindowInterop.SetActivatable(Handle, active);
        if (active) Activate();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        // Klik di aplikasi lain: akhiri input supaya panel bisa menutup lagi.
        TodoModuleView.EndInput();
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

        // Win + Alt + C membuka/menutup notch (Settings.dc.html).
        HwndSource.FromHwnd(Handle)?.AddHook(WndProc);
        HotkeyRegistered = NotchWindowInterop.RegisterWinAltHotkey(Handle, HotkeyId, 'C');
    }

    private const int HotkeyId = 0xC31A;

    public bool HotkeyRegistered { get; private set; }

    /// <summary>Pintasan Win + Alt + C ditekan.</summary>
    public event Action? HotkeyPressed;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NotchWindowInterop.WM_HOTKEY && (int)wParam == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (Handle != IntPtr.Zero) NotchWindowInterop.UnregisterHotkey(Handle, HotkeyId);
        base.OnClosed(e);
    }

    private double _horizontalOffset;

    /// <summary>Geser dari tengah layar (DIP), mis. untuk menghindari webcam.</summary>
    public void SetHorizontalOffset(double dips)
    {
        _horizontalOffset = dips;
        Reposition();
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
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        NotchWindowInterop.PlaceTopCenter(Handle, NotchWindowInterop.GetPrimaryMonitor(), (int)Math.Round(_horizontalOffset * scale));
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
            case NotchState.Collapsed: ToRest(from, slim: false); break;
            case NotchState.Slim: ToRest(from, slim: true); break;
            case NotchState.Peek: ToPeek(from); break;
            case NotchState.Hidden: ToHidden(); break;
        }
        if (from == NotchState.Expanded)
        {
            _bannerTimer.Stop();
            PeekBanner.BeginAnimation(OpacityProperty, null);
            PeekBanner.Opacity = 0;
            PeekBanner.Visibility = Visibility.Collapsed;
        }
        if (from == NotchState.Expanded || to == NotchState.Expanded)
            NotifyVisibleModule();
        UpdateBodyTint();
    }

    /// <summary>Umpan balik langsung saat kursor menyentuh notch, sebelum jeda hover selesai.</summary>
    public void ShowHoverAck(bool hovering)
    {
        var (normal, hover, radius) = _visualState switch
        {
            NotchState.Collapsed => (CollapsedSize,
                new Size(CollapsedSize.Width + HoverGrow.Width, CollapsedSize.Height + HoverGrow.Height), CollapsedRadius),
            NotchState.Slim => (SlimSize, SlimHoverSize, SlimRadius),
            _ => (Size.Empty, Size.Empty, default),
        };
        if (normal.IsEmpty) return;

        if (hovering)
            AnimateShape(hover, radius);
        else
            AnimateShape(normal, radius);
    }

    // ── Transisi ────────────────────────────────────────────────────────────

    private void ToExpanded()
    {
        Kedip.StopIdle();
        HeaderEyes.StartIdle();
        RefreshStatusBar();
        FadeOut(CollapsedLayer, Motion.Ms(80));
        ScaleCollapsed(0.96, Motion.Ms(80), Motion.In);
        FadeOut(PeekLayer, Motion.Ms(80));
        AnimateShoulders(1);

        AnimateShape(ExpandedShape(), ExpandedRadius);

        UpdateClock();
        ExpandedLayer.Visibility = Visibility.Visible;
        FadeIn(ExpandedLayer, Motion.Ms(180), delayMs: 120);
        Slide(StatusShift, TranslateTransform.YProperty, from: -6, Motion.Ms(180), delayMs: 120);
        var mode = ModeView(_moduleIndex);
        mode.Visibility = Visibility.Visible;
        mode.BeginAnimation(OpacityProperty, null);
        mode.Opacity = 1;
        ModeY(mode).Set(10);
        ModeY(mode).AnimateTo(0, delayMs: 120);
    }

    /// <summary>Kembali ke state istirahat: pil (Collapsed) atau garis tipis (Slim).</summary>
    private void ToRest(NotchState from, bool slim)
    {
        _clockTimer.Stop();
        HeaderEyes.StopIdle();
        ShowWindowIfHidden();

        var size = slim ? SlimSize : CollapsedSize;
        var radius = slim ? SlimRadius : CollapsedRadius;

        if (from == NotchState.Hidden)
        {
            // Turun kembali dari atas layar.
            SetShapeImmediately(size, radius);
            ShowLayerImmediately(slim ? null : CollapsedLayer);
            SetShouldersImmediately(slim ? 0 : 1);
            _hideY.AnimateTo(0);
            ShapeHost.Animate(OpacityProperty, Motion.To(1, Motion.Ms(120), Motion.Out));
        }
        else
        {
            double contentDelay;
            switch (from)
            {
                case NotchState.Expanded:
                    FadeOut(ExpandedLayer, Motion.Fast);
                    Slide(StatusShift, TranslateTransform.YProperty, from: null, Motion.Fast, to: -4, ease: Motion.In);
                    AnimateShape(size, radius);
                    contentDelay = 200;
                    break;
                case NotchState.Peek:
                    FadeOut(PeekLayer, Motion.Fast);
                    AnimateShape(size, radius);
                    contentDelay = 140;
                    break;
                default: // Collapsed ↔ Slim
                    AnimateShape(size, radius);
                    contentDelay = 120;
                    break;
            }

            if (slim)
            {
                FadeOut(CollapsedLayer, Motion.Ms(100));
                AnimateShoulders(0);
            }
            else
            {
                FadeIn(CollapsedLayer, Motion.Base, contentDelay);
                ScaleCollapsed(1, Motion.Base, Motion.Out, contentDelay);
                AnimateShoulders(1);
                if (from == NotchState.Expanded && _activity == CollapsedActivity.Idle) Kedip.Blink(delayMs: 360);
            }
        }

        UpdateKedipIdle();
    }

    /// <summary>Kedip hanya berkedip/bernapas saat ia benar-benar terlihat.</summary>
    private void UpdateKedipIdle()
    {
        if (_visualState == NotchState.Collapsed && _activity == CollapsedActivity.Idle)
        {
            Kedip.StartIdle();
            Kedip.BreatheOnce();
        }
        else
        {
            Kedip.StopIdle();
        }
    }

    /// <summary>
    /// Ganti isi pil. Saat pil terlihat: lebar berubah 300 ms ease-spring-soft dan isi crossfade
    /// 160 ms (Collapsed.dc.html). Musik + fokus: musik di kiri, timer ringkas di kanan.
    /// </summary>
    public void SetCollapsedActivity(CollapsedActivity activity)
    {
        if (_activity == activity) return;
        _activity = activity;

        bool combined = activity == CollapsedActivity.MusicAndFocus;
        MusicPillView.SetCompact(combined);
        FocusPillView.SetCompact(combined);
        FocusPillView.HorizontalAlignment = combined ? HorizontalAlignment.Right : HorizontalAlignment.Center;
        FocusPillView.Margin = new Thickness(0, 0, combined ? 12 : 0, 0);
        WaterPillView.SetPulsing(activity == CollapsedActivity.Water && _visualState == NotchState.Collapsed);
        UpdatePillView.SetVisible(activity == CollapsedActivity.Update && _visualState == NotchState.Collapsed);

        var wanted = new HashSet<UIElement>(activity switch
        {
            CollapsedActivity.Music => [MusicPillView],
            CollapsedActivity.Focus => [FocusPillView],
            CollapsedActivity.MusicAndFocus => [MusicPillView, FocusPillView],
            CollapsedActivity.Water => [WaterPillView],
            CollapsedActivity.Update => [UpdatePillView],
            _ => new UIElement[] { Kedip },
        });

        bool visible = _visualState == NotchState.Collapsed;
        if (visible)
            AnimateShape(CollapsedSize, CollapsedRadius);

        foreach (var element in new UIElement[] { Kedip, MusicPillView, FocusPillView, WaterPillView, UpdatePillView })
        {
            bool show = wanted.Contains(element);
            if (visible)
            {
                if (show && (element.Visibility != Visibility.Visible || element.Opacity < 1)) FadeIn(element, Motion.Ms(160), delayMs: 80);
                else if (!show) FadeOut(element, Motion.Ms(160));
            }
            else
            {
                // Tidak terlihat sekarang; lapisan collapsed menampilkan isi yang benar saat kembali.
                element.BeginAnimation(OpacityProperty, null);
                element.Opacity = show ? 1 : 0;
                element.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        UpdateKedipIdle();
    }

    public CollapsedActivity CollapsedActivity => _activity;

    private void ToPeek(NotchState from)
    {
        ShowWindowIfHidden();
        if (from == NotchState.Hidden)
        {
            // Peek penting saat fullscreen: turunkan notch langsung ke bentuk peek.
            _hideY.AnimateTo(0);
            ShapeHost.Animate(OpacityProperty, Motion.To(1, Motion.Ms(120), Motion.Out));
        }

        FadeOut(CollapsedLayer, Motion.Ms(100));
        AnimateShoulders(1);
        AnimateShape(_peekSize, PeekRadius);
        PeekContent.BeginAnimation(OpacityProperty, null);
        PeekContent.Opacity = 1;

        PeekLayer.Visibility = Visibility.Visible;
        FadeIn(PeekLayer, Motion.Base, delayMs: 160);
        Slide(PeekShift, TranslateTransform.XProperty, from: -6, Motion.Base, delayMs: 160);
    }

    /// <summary>
    /// Isi peek berikutnya. Dipanggil sebelum state berpindah ke Peek, atau saat peek sedang tampil:
    /// sejenis → hanya nilai yang bergerak; beda jenis → crossfade 120 ms lalu lebar menyesuaikan.
    /// </summary>
    public void SetPeek(PeekRequest request)
    {
        _peekSize = new Size(request.Width, PeekSize.Height);
        PeekLayer.Width = request.Width;

        bool showing = _visualState == NotchState.Peek;
        if (!showing || PeekContent.Kind == request.Kind || Motion.Reduced)
        {
            PeekContent.Show(request);
            if (showing) AnimateShape(_peekSize, PeekRadius);
            return;
        }

        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0, Motion.ReducedCrossfade) { EasingFunction = Motion.In };
        fadeOut.Completed += (_, _) =>
        {
            PeekContent.Show(request);
            PeekContent.Animate(OpacityProperty, Motion.To(1, Motion.ReducedCrossfade, Motion.Out));
        };
        PeekContent.Animate(OpacityProperty, fadeOut);
        AnimateShape(_peekSize, PeekRadius);
    }

    /// <summary>Peek saat panel terbuka: baris 32 px di bawah bar tab, hilang setelah masa tahan.</summary>
    public void ShowBanner(PeekRequest request)
    {
        PeekBannerContent.Show(request);
        PeekBanner.Visibility = Visibility.Visible;
        PeekBanner.Animate(OpacityProperty, Motion.To(1, Motion.Fast, Motion.Out));
        _bannerTimer.Stop();
        _bannerTimer.Interval = request.Hold;
        _bannerTimer.Start();
    }

    private void HideBanner()
    {
        _bannerTimer.Stop();
        if (PeekBanner.Visibility != Visibility.Visible) return;
        FadeOut(PeekBanner, Motion.Fast);
    }

    private void ToHidden()
    {
        _clockTimer.Stop();
        Kedip.StopIdle();
        HeaderEyes.StopIdle();

        double distance = Body.ActualHeight + 12;
        _hideY.Settled += () =>
        {
            // Sembunyikan jendela sungguhan supaya tidak menumpuk di atas game/video fullscreen.
            if (_visualState == NotchState.Hidden) Hide();
        };
        _hideY.AnimateTo(-distance);
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

    private void AnimateShape(Size size, CornerRadius radius)
    {
        _width.AnimateTo(size.Width);
        _height.AnimateTo(size.Height);
        _radius.AnimateTo(radius.BottomLeft);
    }

    private void SetShapeImmediately(Size size, CornerRadius radius)
    {
        _width.Set(size.Width);
        _height.Set(size.Height);
        _radius.Set(radius.BottomLeft);
    }

    /// <summary>Bahu cekung ikut menyusut di mode tipis supaya garisnya bersih.</summary>
    private void AnimateShoulders(double scale) => _shoulders.AnimateTo(scale);

    private void SetShouldersImmediately(double scale) => _shoulders.Set(scale);

    private void ShowLayerImmediately(UIElement? layer)
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
        if (_visualState == NotchState.Expanded) RefreshStatusBar();
        _clockTimer.Interval = TimeSpan.FromSeconds(60 - now.Second) + TimeSpan.FromMilliseconds(50);
        if (_visualState == NotchState.Expanded) _clockTimer.Start();
    }
}
