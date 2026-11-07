// Tampilan island (0.3.0), diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): tiap
// tampilan punya ukurannya sendiri, notch berubah bentuk mengikutinya dengan pegas, dan gulir mouse
// berpindah tampilan.

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Peeko.App.Animation;
using Peeko.App.Lyrics;
using Peeko.App.Music;
using Peeko.Core.Notch;
using Peeko.Core.Status;
using Peeko.Platform.Control;

namespace Peeko.App.Notch;

public partial class NotchWindow
{
    public const int MusicTab = 0;
    public const int ControlTab = 1;
    public const int SystemTab = 2;
    public const int TodayTab = 3;
    public const int PrayerTab = 4;
    public const int TodoTab = 5;
    public const int HealthTab = 6;
    public const int NotesTab = 7;
    public const int LauncherTab = 8;
    public const int ShelfTab = 9;
    public const int ClipboardTab = 10;
    /// <summary>Pilihan zona saat jendela diseret ke notch; bukan tab biasa (tanpa titik, tidak ikut digulir).</summary>
    public const int RadioTab = 11;
    public const int SnapTab = 12;
    /// <summary>Permintaan izin Claude Code; seperti Snap, bukan tab biasa.</summary>
    public const int ClaudeTab = 13;
    public const int AiTab = 14;
    private const int ModeCount = 15;

    // Di atas isi: 6 + baris status 24 + 2 + titik 6 + 12; di bawahnya 16. Isi diberi tepi 18.
    private const double ChromeHeight = 6 + 24 + 2 + 6 + 12 + 16;
    private const double SideMargin = 18;
    // Satu langkah roda (120) = satu tampilan; touchpad mengirim langkah kecil yang dijumlahkan dulu.
    private const double WheelStep = 120;
    private static readonly TimeSpan WheelIdleReset = TimeSpan.FromMilliseconds(400);

    private readonly bool[] _modeEnabled = [true, true, true, true, true, true, true, true, true, true, false, false, false, false, false];
    private bool _lyricsEnabled = true, _calendarEnabled = true, _focusEnabled = true, _waterEnabled = true, _healthEnabled = true;
    private readonly Dictionary<FrameworkElement, SpringValue> _modeY = [];
    private readonly List<(Border Dot, SpringValue Width)> _dots = [];
    private DateTime _lastWheel;
    private double _wheelAccumulated;
    private Today.WeatherService? _weather;

    /// <summary>Tampilan yang dipilih (walau panel sedang tertutup).</summary>
    public int SelectedModule => _moduleIndex;

    /// <summary>Tampilan yang benar-benar terlihat sekarang, atau −1 jika panel tertutup.</summary>
    public int VisibleModule => _visualState == NotchState.Expanded ? _moduleIndex : -1;

    /// <summary>Panel dibuka/ditutup atau tampilan berganti.</summary>
    public event Action<int>? VisibleModuleChanged;

    public void AttachWeather(Today.WeatherService weather) => _weather = weather;

    private FrameworkElement ModeView(int index) => index switch
    {
        MusicTab => MusicMode,
        ControlTab => ControlModuleView,
        SystemTab => SystemModuleView,
        TodayTab => TodayMode,
        PrayerTab => PrayerModuleView,
        TodoTab => TodoModuleView,
        HealthTab => HealthMode,
        NotesTab => NotesModuleView,
        LauncherTab => LauncherModuleView,
        ClipboardTab => ClipboardModuleView,
        SnapTab => SnapModuleView,
        ClaudeTab => ClaudeModuleView,
        AiTab => AiModuleView,
        RadioTab => RadioModuleView,
        _ => ShelfModuleView,
    };

    /// <summary>Lebar × tinggi isi tampilan; notch = isi + tepi + baris status + titik.</summary>
    private Size ModeContentSize(int index)
    {
        switch (index)
        {
            case MusicTab:
                MusicModuleView.Measure(new Size(400, double.PositiveInfinity));
                double lyrics = LyricsHeight();
                LyricsModuleView.Visibility = lyrics > 0 ? Visibility.Visible : Visibility.Collapsed;
                return new Size(400, MusicModuleView.DesiredSize.Height + (lyrics > 0 ? 10 + lyrics : 0));
            case ControlTab:
                return new Size(400, 196);
            case SystemTab:
                return new Size(440, 150);
            case ClipboardTab:
                return new Size(440, 150);
            case RadioTab:
                return new Size(440, 150);
            case AiTab:
                return new Size(Sistem.AiModule.ContentWidth, Sistem.AiModule.ContentHeight);
            case ClaudeTab:
                return new Size(Sistem.ClaudeModule.ContentWidth, Sistem.ClaudeModule.ContentHeight);
            case SnapTab:
                return new Size(Sistem.SnapModule.ContentWidth, Sistem.SnapModule.ContentHeight);
            case LauncherTab:
                LauncherModuleView.Height = LauncherModuleView.ContentHeight;
                return new Size(440, LauncherModuleView.ContentHeight);
            case TodayTab:
                double width = (_calendarEnabled ? 196 : 0) + (_focusEnabled ? 224 : 0) + (_calendarEnabled && _focusEnabled ? 20 : 0);
                double height = Math.Max(_calendarEnabled ? TodayModuleView.ContentHeight : 0, _focusEnabled ? Productivity.FocusModule.ContentHeight : 0);
                double agenda = AgendaView.ContentHeight;
                AgendaView.Width = Math.Max(width, 400);
                return new Size(Math.Max(width, agenda > 0 ? 400 : 0), height + agenda);
            case PrayerTab:
                return new Size(400, 148);
            case TodoTab:
                return new Size(400, 150);
            case HealthTab:
                return new Size((_waterEnabled ? 150 : 0) + (_healthEnabled ? 250 : 0) + (_waterEnabled && _healthEnabled ? 14 : 0), 150);
            case NotesTab:
                return new Size(440, 170);
            default:
                // Rak kosong cukup zona drop; berisi: judul + kartu; Ubah format butuh ruang chip.
                double shelf = ShelfModuleView.PhoneActive ? 136
                    : ShelfModuleView.ConvertPanel.IsActive ? 160
                    : ShelfModuleView.FilledView.Visibility == Visibility.Visible ? 124 : 96;
                ShelfModuleView.Height = shelf;
                return new Size(440, shelf);
        }
    }

    /// <summary>Pita lirik: 66 untuk lirik/memuat, 28 untuk pesan satu baris, 0 jika tidak perlu.</summary>
    private double LyricsHeight()
    {
        if (!_lyricsEnabled || LyricsModuleView.DataContext is not LyricsViewModel vm) return 0;
        return vm.State switch
        {
            LyricsViewState.Synced or LyricsViewState.Plain or LyricsViewState.Loading => 66,
            LyricsViewState.NotFound or LyricsViewState.Failed or LyricsViewState.Instrumental or LyricsViewState.Disabled => 28,
            _ => 0,
        };
    }

    private Size ExpandedShape()
    {
        var content = ModeContentSize(_moduleIndex);
        ModuleHost.Height = content.Height;
        return new Size(Math.Max(320, content.Width + SideMargin * 2), content.Height + ChromeHeight);
    }

    /// <summary>Isi tampilan berubah ukuran (lirik muncul, modul dimatikan…): notch ikut menyesuaikan.</summary>
    private void RefreshExpandedShape()
    {
        if (_visualState != NotchState.Expanded) return;
        AnimateShape(ExpandedShape(), ExpandedRadius);
    }

    private void WatchContentSize()
    {
        ShelfModuleView.FilledView.IsVisibleChanged += (_, _) => RefreshExpandedShape();
        LauncherModuleView.SizeChanged += RefreshExpandedShape;
        ShelfModuleView.PhoneChanged += RefreshExpandedShape;
        AgendaView.SizeChanged2 += RefreshExpandedShape;
        ShelfModuleView.ConvertPanel.IsVisibleChanged += (_, _) => RefreshExpandedShape();
        if (LyricsModuleView.DataContext is INotifyPropertyChanged lyrics)
            lyrics.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(LyricsViewModel.State)) RefreshExpandedShape(); };
        if (MusicModuleView.DataContext is INotifyPropertyChanged music)
            music.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(MusicViewModel.HasSession) or nameof(MusicViewModel.HasTimeline) or nameof(MusicViewModel.CanSeek))
                    Dispatcher.BeginInvoke(RefreshExpandedShape, System.Windows.Threading.DispatcherPriority.Loaded);
            };
    }

    private void NotifyVisibleModule()
    {
        MusicModuleView.SetActive(VisibleModule == MusicTab);
        LyricsModuleView.SetVisible(VisibleModule == MusicTab);
        ControlModuleView.SetVisible(VisibleModule == ControlTab);
        SystemModuleView.SetVisible(VisibleModule == SystemTab);
        ClipboardModuleView.SetVisible(VisibleModule == ClipboardTab);
        RadioModuleView.SetVisible(VisibleModule == RadioTab);
        AiModuleView.SetVisible(VisibleModule == AiTab);
        if (VisibleModule == TodayTab) TodayModuleView.Refresh();
        if (VisibleModule == PrayerTab) PrayerModuleView.Refresh();
        if (VisibleModule != TodoTab) TodoModuleView.EndInput();
        if (VisibleModule != NotesTab) NotesModuleView.EndInput();
        if (VisibleModule != LauncherTab) LauncherModuleView.EndInput();
        if (VisibleModule != HealthTab) HealthModuleView.StopBreath();
        else HealthModuleView.Refresh();
        VisibleModuleChanged?.Invoke(VisibleModule);
    }

    /// <summary>Bagian yang dimatikan di Pengaturan disembunyikan; tampilan kosong dilewati saat menggulir.</summary>
    public void SetModulesEnabled(bool music, bool lyrics, bool control, bool system, bool calendar, bool focus, bool prayer, bool todo,
        bool water, bool health, bool notes, bool launcher, bool shelf, bool clipboard, bool radio, bool ai)
    {
        _lyricsEnabled = lyrics;
        _calendarEnabled = calendar;
        _focusEnabled = focus;
        _waterEnabled = water;
        _healthEnabled = health;
        TodayModuleView.Visibility = calendar ? Visibility.Visible : Visibility.Collapsed;
        FocusModuleView.Visibility = focus ? Visibility.Visible : Visibility.Collapsed;
        FocusModuleView.Margin = new Thickness(calendar ? 20 : 0, 0, 0, 0);
        WaterModuleView.Visibility = water ? Visibility.Visible : Visibility.Collapsed;
        HealthModuleView.Visibility = health ? Visibility.Visible : Visibility.Collapsed;
        HealthModuleView.Margin = new Thickness(water ? 14 : 0, 0, 0, 0);

        _modeEnabled[MusicTab] = music;
        _modeEnabled[ControlTab] = control;
        _modeEnabled[SystemTab] = system;
        _modeEnabled[LauncherTab] = launcher;
        _modeEnabled[ClipboardTab] = clipboard;
        _modeEnabled[RadioTab] = radio;
        _modeEnabled[AiTab] = ai;
        _modeEnabled[TodayTab] = calendar || focus;
        _modeEnabled[PrayerTab] = prayer;
        _modeEnabled[TodoTab] = todo;
        _modeEnabled[HealthTab] = water || health;
        _modeEnabled[NotesTab] = notes;
        _modeEnabled[ShelfTab] = shelf;
        BuildDots();
        if (!_modeEnabled[_moduleIndex])
        {
            int first = Array.IndexOf(_modeEnabled, true);
            if (first >= 0) SelectModule(first);
        }
        RefreshExpandedShape();
    }

    public bool IsModuleEnabled(int index) => _modeEnabled[index];

    private int _beforeSnap = MusicTab;

    /// <summary>Sedang menampilkan pilihan zona snap.</summary>
    public bool Snapping { get; private set; }

    public Sistem.SnapModule SnapModule => SnapModuleView;

    /// <summary>Tampilkan/sembunyikan pilihan zona; setelahnya kembali ke tampilan sebelumnya.</summary>
    public void ShowSnap(bool on)
    {
        if (on == Snapping) return;
        Snapping = on;
        if (on)
        {
            _beforeSnap = _moduleIndex;
            _modeEnabled[SnapTab] = true;
            SelectModule(SnapTab);
            return;
        }
        SnapModuleView.Reset();
        _modeEnabled[SnapTab] = false;
        int back = _modeEnabled[_beforeSnap] ? _beforeSnap : Array.IndexOf(_modeEnabled, true);
        if (back >= 0) SelectModule(back);
    }

    public Sistem.ClaudeModule ClaudeModule => ClaudeModuleView;

    private int _beforeClaude = MusicTab;

    /// <summary>Sedang menampilkan permintaan izin Claude Code.</summary>
    public bool AskingClaude { get; private set; }

    public void ShowClaude(bool on)
    {
        if (on == AskingClaude) return;
        AskingClaude = on;
        if (on)
        {
            if (!Snapping) _beforeClaude = _moduleIndex;
            _modeEnabled[ClaudeTab] = true;
            SelectModule(ClaudeTab);
            return;
        }
        ClaudeModuleView.Hide();
        _modeEnabled[ClaudeTab] = false;
        int back = _modeEnabled[_beforeClaude] ? _beforeClaude : Array.IndexOf(_modeEnabled, true);
        if (back >= 0) SelectModule(back);
    }

    /// <summary>Kursor (piksel layar) berada di atas atau dekat notch?</summary>
    public bool IsCursorNear(int screenX, int screenY)
    {
        if (PresentationSource.FromVisual(Body) is null) return false;
        var p = Body.PointFromScreen(new Point(screenX, screenY));
        return p.X >= -40 && p.X <= Body.ActualWidth + 40 && p.Y >= -20 && p.Y <= Body.ActualHeight + 30;
    }

    /// <summary>Pilih tampilan (walau panel tertutup, mis. buka Musik saat lagu diputar).</summary>
    public void SelectModule(int index)
    {
        if (index == _moduleIndex || index < 0 || index >= ModeCount || !_modeEnabled[index]) return;

        int direction = index > _moduleIndex ? 1 : -1;
        var hide = ModeView(_moduleIndex);
        var show = ModeView(index);
        _moduleIndex = index;
        UpdateDots();

        bool animate = !Motion.Reduced && _visualState == NotchState.Expanded;
        const double distance = 28;

        if (animate)
        {
            // Bloom: isi lama bergeser keluar searah gulir sambil memudar, isi baru masuk dari sisi lain,
            // dan bentuk notch berubah ke ukuran tampilan baru dengan pegas.
            ModeY(hide).AnimateTo(-distance * direction);
            var fadeOut = Motion.To(0, Motion.Fast, Motion.Standard);
            fadeOut.Completed += (_, _) =>
            {
                if (ModeView(_moduleIndex) != hide) hide.Visibility = Visibility.Collapsed;
            };
            hide.Animate(OpacityProperty, fadeOut);

            show.Visibility = Visibility.Visible;
            var y = ModeY(show);
            if (!y.IsMoving) y.Set(distance * direction);
            y.AnimateTo(0);
            var fadeIn = Motion.To(1, Motion.Ms(220), Motion.Out, delayMs: 40);
            fadeIn.From = 0;
            show.Animate(OpacityProperty, fadeIn);
            AnimateShape(ExpandedShape(), ExpandedRadius);
        }
        else
        {
            hide.BeginAnimation(OpacityProperty, null);
            hide.Visibility = Visibility.Collapsed;
            show.BeginAnimation(OpacityProperty, null);
            show.Opacity = 1;
            show.Visibility = Visibility.Visible;
            ModeY(show).Set(0);
        }

        NotifyVisibleModule();
    }

    private SpringValue ModeY(FrameworkElement mode)
    {
        if (_modeY.TryGetValue(mode, out var spring)) return spring;
        var t = new TranslateTransform();
        mode.RenderTransform = t;
        spring = new SpringValue(0, SpringConfig.Content, v => t.Y = v);
        _modeY[mode] = spring;
        return spring;
    }

    private int NextEnabled(int from, int step)
    {
        for (int i = from + step; i >= 0 && i < ModeCount; i += step)
            if (_modeEnabled[i]) return i;
        return -1;
    }

    /// <summary>
    /// Gulir di mana saja di panel untuk pindah tampilan. Setiap langkah roda dihitung (tidak ada yang
    /// dibuang), jadi gulir cepat melompati beberapa tampilan sekaligus; pegas meneruskan gerak yang
    /// sedang berjalan sehingga perpindahan beruntun tetap mulus. Daftar yang masih bisa digulir
    /// (tugas, rak, lirik biasa) menggulir isinya dulu.
    /// </summary>
    private void OnExpandedWheel(object sender, MouseWheelEventArgs e)
    {
        if (_visualState != NotchState.Expanded) return;
        int step = e.Delta < 0 ? 1 : -1;
        if (CanScrollInside(e.OriginalSource as DependencyObject, step)) return;
        e.Handled = true;

        var now = DateTime.UtcNow;
        // Arah berbalik atau jeda lama: mulai hitungan baru supaya sisa gulir lama tidak ikut terbawa.
        if (now - _lastWheel > WheelIdleReset || Math.Sign(_wheelAccumulated) == step) _wheelAccumulated = 0;
        _lastWheel = now;
        _wheelAccumulated += e.Delta;

        int target = _moduleIndex;
        while (Math.Abs(_wheelAccumulated) >= WheelStep * 0.9)
        {
            int next = NextEnabled(target, step);
            _wheelAccumulated -= Math.Sign(_wheelAccumulated) * WheelStep;
            if (next < 0)
            {
                _wheelAccumulated = 0; // sudah di ujung
                break;
            }
            target = next;
        }
        if (Math.Abs(_wheelAccumulated) < 1) _wheelAccumulated = 0;
        if (target != _moduleIndex) SelectModule(target);
    }

    // ── Layar sentuh ──
    // Sentuhan tidak punya hover: ketuk notch langsung membuka panel, dan geser kiri/kanan di
    // panel berpindah tampilan seperti gulir mouse.

    /// <summary>Notch diketuk dengan jari/pena saat belum terbuka.</summary>
    public event Action? TouchOpenRequested;

    private Point? _swipeStart;
    private const double SwipeMin = 60;

    private void WatchTouch()
    {
        ShapeHost.PreviewTouchDown += (_, e) =>
        {
            if (_visualState != NotchState.Expanded)
            {
                // Tidak di-Handled: sentuhan tetap dipromosikan jadi mouse, jadi ketukan di luar
                // notch nanti memicu MouseLeave dan panel menutup seperti biasa.
                TouchOpenRequested?.Invoke();
                return;
            }
            _swipeStart = e.GetTouchPoint(this).Position;
        };
        ShapeHost.PreviewTouchUp += (_, e) =>
        {
            if (_swipeStart is not { } start || _visualState != NotchState.Expanded) return;
            _swipeStart = null;
            var end = e.GetTouchPoint(this).Position;
            double dx = end.X - start.X, dy = end.Y - start.Y;
            if (Math.Abs(dx) < SwipeMin || Math.Abs(dx) < Math.Abs(dy) * 2) return;
            int next = NextEnabled(_moduleIndex, dx < 0 ? 1 : -1);
            if (next >= 0)
            {
                SelectModule(next);
                e.Handled = true;
            }
        };
    }

    private bool CanScrollInside(DependencyObject? source, int step)
    {
        for (var node = source; node is not null && node != ModuleHost; node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node))
        {
            if (node is not ScrollViewer sv) continue;
            if (sv.ScrollableHeight > 0)
                return step > 0 ? sv.VerticalOffset < sv.ScrollableHeight - 0.5 : sv.VerticalOffset > 0.5;
            if (sv.ScrollableWidth > 0)
                return step > 0 ? sv.HorizontalOffset < sv.ScrollableWidth - 0.5 : sv.HorizontalOffset > 0.5;
        }
        return false;
    }

    // ── Titik halaman ──────────────────────────────────────────────────────

    private void BuildDots()
    {
        Dots.Children.Clear();
        _dots.Clear();
        string[] names = ["Musik", "Kontrol", "Sistem", "Hari ini", "Salat", "Tugas", "Sehat", "Catatan", "Aplikasi", "Rak", "Clipboard", "Radio", "Snap", "Claude", "AI"];
        for (int i = 0; i < ModeCount; i++)
        {
            if (!_modeEnabled[i] || i is SnapTab or ClaudeTab) continue;
            int index = i;
            var dot = new Border
            {
                Height = 6,
                Width = 6,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(_dots.Count == 0 ? 0 : 5, 0, 0, 0),
                Background = (Brush)FindResource("Peeko.Brush.SurfacePressed"),
                Cursor = Cursors.Hand,
                ToolTip = names[i],
                Tag = index,
            };
            // Area klik lebih besar dari titiknya.
            var hit = new Border { Background = Brushes.Transparent, Padding = new Thickness(0, 6, 0, 6), Margin = new Thickness(0, -6, 0, -6), Child = dot };
            hit.MouseLeftButtonUp += (_, e) => { SelectModule(index); e.Handled = true; };
            Dots.Children.Add(hit);
            _dots.Add((dot, new SpringValue(6, SpringConfig.Content, v => dot.Width = Math.Max(6, v))));
        }
        Dots.Visibility = _dots.Count > 1 ? Visibility.Visible : Visibility.Hidden;
        UpdateDots();
    }

    private void UpdateDots()
    {
        foreach (var (dot, width) in _dots)
        {
            bool active = (int)dot.Tag == _moduleIndex;
            dot.Background = (Brush)FindResource(active ? "Peeko.Brush.TextPrimary" : "Peeko.Brush.SurfacePressed");
            width.AnimateTo(active ? 16 : 6);
        }
    }

    // ── Baris status ───────────────────────────────────────────────────────

    private void RefreshStatusBar()
    {
        var battery = SystemControls.GetBattery();
        BatteryText.Text = battery is { } b ? $"{b.Percent}%" : "–";
        BatteryFill.Width = battery is { } c ? Math.Max(1, 15 * c.Percent / 100.0) : 0;
        BatteryFill.Background = (Brush)FindResource(battery is { Percent: <= 20, PluggedIn: false } ? "Peeko.Brush.Danger"
            : battery is { Charging: true } ? "Peeko.Brush.Accent" : "Peeko.Brush.TextSecondary");
        BatteryBolt.Visibility = battery is { Charging: true } ? Visibility.Visible : Visibility.Collapsed;
        _ = RefreshWeatherAsync();
    }

    private async Task RefreshWeatherAsync()
    {
        if (_weather is null || !_weather.IsEnabled)
        {
            WeatherBox.Visibility = Visibility.Collapsed;
            return;
        }
        WeatherBox.Visibility = Visibility.Visible;
        var now = await _weather.GetAsync();
        if (now is null)
        {
            WeatherText.Text = "–";
            WeatherGlyph.Data = (Geometry)FindResource("Peeko.Icon.Thermo");
            WeatherBox.ToolTip = "Cuaca belum bisa dimuat";
            return;
        }
        WeatherText.Text = $"{now.Temperature}°";
        WeatherBox.ToolTip = _weather.Place is { } place ? $"{now.Condition} · {place}" : now.Condition;
        WeatherGlyph.Data = (Geometry)FindResource(now.Icon switch
        {
            WeatherIcon.Clear => "Peeko.Icon.Sun",
            WeatherIcon.Night => "Peeko.Icon.Moon",
            WeatherIcon.Cloudy => "Peeko.Icon.Cloud",
            WeatherIcon.Fog => "Peeko.Icon.Fog",
            WeatherIcon.Drizzle => "Peeko.Icon.CloudDrizzle",
            WeatherIcon.Rain => "Peeko.Icon.CloudRain",
            WeatherIcon.Snow => "Peeko.Icon.CloudSnow",
            WeatherIcon.Storm => "Peeko.Icon.CloudStorm",
            _ => "Peeko.Icon.Thermo",
        });
    }
}
