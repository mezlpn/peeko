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
    private static readonly Size ExpandedSize = new(560, 260);

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

    public const int MusicTab = 0;
    public const int LyricsTab = 1;

    // Bentuk notch digerakkan pegas, satu per sifat (cara Bloom): lebar, tinggi, radius, bahu, dan
    // posisi sembunyi masing-masing punya kekakuan sendiri dan bisa dibalik di tengah gerak.
    private readonly SpringValue _width;
    private readonly SpringValue _height;
    private readonly SpringValue _radius;
    private readonly SpringValue _shoulders;
    private readonly SpringValue _hideY;
    private readonly Dictionary<FrameworkElement, SpringValue> _moduleX = [];

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

    public FrameworkElement DropTarget => ShapeHost;

    /// <summary>Tab yang dipilih (walau panel sedang tertutup).</summary>
    public int SelectedModule => _moduleIndex;

    /// <summary>Modul yang benar-benar terlihat sekarang, atau −1 jika panel tertutup.</summary>
    public int VisibleModule => _visualState == NotchState.Expanded ? _moduleIndex : -1;

    /// <summary>Panel dibuka/ditutup atau tab berganti.</summary>
    public event Action<int>? VisibleModuleChanged;

    public const int FocusTab = 2;
    public const int WaterTab = 3;
    public const int TodoTab = 4;
    public const int ShelfTab = 5;
    public const int ControlTab = 6;

    private UIElement ModuleView(int index) => index switch
    {
        MusicTab => MusicModuleView,
        LyricsTab => LyricsModuleView,
        FocusTab => FocusModuleView,
        WaterTab => WaterModuleView,
        TodoTab => TodoModuleView,
        ShelfTab => ShelfModuleView,
        ControlTab => ControlModuleView,
        _ => PlaceholderModule,
    };

    private void NotifyVisibleModule()
    {
        MusicModuleView.SetActive(VisibleModule == MusicTab);
        LyricsModuleView.SetVisible(VisibleModule == LyricsTab);
        ControlModuleView.SetVisible(VisibleModule == ControlTab);
        if (VisibleModule != TodoTab) TodoModuleView.EndInput();
        VisibleModuleChanged?.Invoke(VisibleModule);
    }

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

    /// <summary>Modul yang dimatikan di Pengaturan dihapus dari bar tab, tidak sekadar dinonaktifkan.</summary>
    public void SetModuleEnabled(int index, bool enabled)
    {
        if (Tabs.Children[index] is not System.Windows.Controls.RadioButton tab) return;
        tab.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        if (!enabled && _moduleIndex == index)
        {
            // Pindah ke tab pertama yang masih aktif.
            foreach (var child in Tabs.Children.OfType<System.Windows.Controls.RadioButton>())
            {
                if (child.Visibility == Visibility.Visible)
                {
                    child.IsChecked = true;
                    break;
                }
            }
        }
    }

    public bool IsModuleEnabled(int index) =>
        Tabs.Children[index] is UIElement tab && tab.Visibility == Visibility.Visible;

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
        FadeOut(CollapsedLayer, Motion.Ms(80));
        ScaleCollapsed(0.96, Motion.Ms(80), Motion.In);
        FadeOut(PeekLayer, Motion.Ms(80));
        AnimateShoulders(1);

        AnimateShape(ExpandedSize, ExpandedRadius);

        UpdateClock();
        ExpandedLayer.Visibility = Visibility.Visible;
        FadeIn(ExpandedLayer, Motion.Ms(180), delayMs: 120);
        Slide(TabBarShift, TranslateTransform.YProperty, from: -6, Motion.Ms(180), delayMs: 120);
        Slide(ModuleShift, TranslateTransform.YProperty, from: 8, Motion.Ms(240), delayMs: 150);
    }

    /// <summary>Kembali ke state istirahat: pil (Collapsed) atau garis tipis (Slim).</summary>
    private void ToRest(NotchState from, bool slim)
    {
        _clockTimer.Stop();
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
                    Slide(TabBarShift, TranslateTransform.YProperty, from: null, Motion.Fast, to: -4, ease: Motion.In);
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

    /// <summary>Pilih tab sebelum panel terbuka (mis. buka tab Musik saat lagu diputar).</summary>
    public void SelectModule(int index)
    {
        if (Tabs.Children[index] is System.Windows.Controls.RadioButton tab)
            tab.IsChecked = true;
    }

    private void OnTabChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.RadioButton tab || Tabs is null) return;
        int index = Tabs.Children.IndexOf(tab);
        if (index < 0 || index == _moduleIndex) return;

        // Ganti tab (revisi #18): geser seperti umumnya. Isi lama bergeser keluar searah tab sambil
        // memudar, isi baru masuk dari sisi lain sambil muncul; keduanya bergerak bersamaan.
        int direction = index > _moduleIndex ? 1 : -1;
        var hide = (FrameworkElement)ModuleView(_moduleIndex);
        var show = (FrameworkElement)ModuleView(index);
        _moduleIndex = index;

        PlaceholderTitle.Text = tab.ToolTip is string name ? $"Modul {name} menyusul" : "Modul menyusul";

        bool animate = !Motion.Reduced && _visualState == NotchState.Expanded && hide != show;
        const double distance = 48;

        if (hide != show)
        {
            if (animate)
            {
                ModuleX(hide).AnimateTo(-distance * direction);
                var fadeOut = Motion.To(0, Motion.Base, Motion.Standard);
                fadeOut.Completed += (_, _) =>
                {
                    // Bisa saja pengguna sudah kembali ke tab ini sebelum animasi selesai.
                    if (ModuleView(_moduleIndex) != hide) hide.Visibility = Visibility.Collapsed;
                };
                hide.Animate(OpacityProperty, fadeOut);
            }
            else
            {
                hide.BeginAnimation(OpacityProperty, null);
                hide.Visibility = Visibility.Collapsed;
            }
        }

        show.Visibility = Visibility.Visible;
        var shiftIn = ModuleX(show);
        if (animate)
        {
            // Pegas: kalau tab diganti cepat berturut-turut, isi melanjutkan geraknya, tidak meloncat.
            if (!shiftIn.IsMoving) shiftIn.Set(distance * direction);
            shiftIn.AnimateTo(0);
            var fadeIn = Motion.To(1, Motion.Ms(280), Motion.Out);
            fadeIn.From = 0;
            show.Animate(OpacityProperty, fadeIn);
        }
        else
        {
            shiftIn.Set(0);
            show.BeginAnimation(OpacityProperty, null);
            show.Opacity = 1;
        }

        NotifyVisibleModule();
    }

    /// <summary>Gulir di bar tab untuk pindah modul (seperti island Bloom yang berganti mode saat digulir).</summary>
    private void OnTabBarWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        var tabs = Tabs.Children.OfType<System.Windows.Controls.RadioButton>()
            .Where(t => t.Visibility == Visibility.Visible).ToList();
        int current = tabs.FindIndex(t => t.IsChecked == true);
        int next = Math.Clamp(current + (e.Delta < 0 ? 1 : -1), 0, tabs.Count - 1);
        if (next != current) tabs[next].IsChecked = true;
        e.Handled = true;
    }

    /// <summary>Tiap modul punya pegas geser sendiri supaya yang keluar dan yang masuk bisa bergerak bersamaan.</summary>
    private SpringValue ModuleX(FrameworkElement module)
    {
        if (_moduleX.TryGetValue(module, out var spring)) return spring;
        var t = new TranslateTransform();
        module.RenderTransform = t;
        spring = new SpringValue(0, SpringConfig.Content, v => t.X = v);
        _moduleX[module] = spring;
        return spring;
    }

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
        _clockTimer.Interval = TimeSpan.FromSeconds(60 - now.Second) + TimeSpan.FromMilliseconds(50);
        if (_visualState == NotchState.Expanded) _clockTimer.Start();
    }
}
