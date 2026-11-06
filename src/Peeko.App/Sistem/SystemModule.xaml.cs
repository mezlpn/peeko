using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Peeko.App.Settings;
using Peeko.Core.Status;
using Peeko.Platform.Power;

namespace Peeko.App.Sistem;

/// <summary>
/// Isi tab Sistem. Statistik dibaca tiap 2 detik hanya selama tab terlihat (GPU/suhu tiap 4 detik
/// karena lewat WMI); begitu tab ditutup timer berhenti.
/// </summary>
public partial class SystemModule : UserControl
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private readonly SystemStats _stats = new();
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = Interval };
    private readonly Dictionary<string, (Border Fill, TextBlock Value, FrameworkElement Row)> _bars = [];
    private ScreenTimeController? _screen;
    private SettingsService? _settings;
    private bool _visible;
    private bool _reading;
    private int _tick;
    private bool _showScreen;
    private bool _rendering;

    public SystemModule()
    {
        InitializeComponent();
        foreach (var (key, label) in new[] { ("cpu", "CPU"), ("ram", "RAM"), ("gpu", "GPU"), ("disk", "Disk"), ("temp", "Suhu") })
            StatsPanel.Children.Add(BarRow(key, label, _bars));
        _bars["temp"].Row.Visibility = Visibility.Collapsed;
        _timer.Tick += (_, _) => _ = ReadAsync();
    }

    public void Attach(ScreenTimeController screen, SettingsService settings)
    {
        _screen = screen;
        _settings = settings;
        // Menampilkan screen time ikut mencatat waktu berjalan (yang memicu Updated lagi): jangan berulang.
        screen.Updated += () =>
        {
            if (_visible && _showScreen && !_rendering) ShowRight();
        };
    }

    public void SetVisible(bool visible)
    {
        if (visible == _visible) return;
        _visible = visible;
        if (visible)
        {
            _tick = 0;
            _ = ReadAsync();
            _timer.Start();
            ShowRight();
        }
        else
        {
            _timer.Stop();
        }
    }

    private async Task ReadAsync()
    {
        if (_reading) return;
        _reading = true;
        try
        {
            bool slow = _tick++ % 2 == 0;
            var s = await Task.Run(() => _stats.Read(slow));
            if (!_visible) return;
            Set("cpu", s.Cpu, $"{s.Cpu * 100:0}%");
            Set("ram", s.Ram, $"{s.RamUsedGb:0.0}/{s.RamTotalGb:0} GB");
            if (s.Gpu is { } gpu) Set("gpu", gpu, $"{gpu * 100:0}%");
            else _bars["gpu"].Row.Visibility = Visibility.Collapsed;
            if (s.DiskFree is { } free && s.DiskTotalGb > 0)
                Set("disk", 1 - free / s.DiskTotalGb, $"sisa {free:0} GB");
            if (s.TemperatureC is { } t)
            {
                _bars["temp"].Row.Visibility = Visibility.Visible;
                Set("temp", Math.Clamp((t - 30) / 70, 0, 1), $"{t:0}°C", warn: t >= 85);
            }
            var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
            UptimeText.Text = up.TotalDays >= 1 ? $"menyala {(int)up.TotalDays} h {up.Hours} j" : $"menyala {up.Hours} j {up.Minutes} m";
            if (!_showScreen) ShowProgress();
        }
        finally
        {
            _reading = false;
        }
    }

    private void Set(string key, double fraction, string value, bool warn = false)
    {
        var (fill, text, row) = _bars[key];
        row.Visibility = Visibility.Visible;
        fill.Width = Math.Max(0, Math.Clamp(fraction, 0, 1) * BarWidth(fill));
        fill.Background = B(warn || fraction >= 0.9 ? "Warning" : "Accent");
        text.Text = value;
    }

    private static double BarWidth(Border fill) => fill.Parent is Grid g ? g.ActualWidth : 0;

    // ── Kanan: progres / screen time ───────────────────────────────────────

    private void OnProgressTab(object sender, MouseButtonEventArgs e)
    {
        _showScreen = false;
        ShowRight();
    }

    private void OnScreenTab(object sender, MouseButtonEventArgs e)
    {
        _showScreen = true;
        ShowRight();
    }

    private readonly Dictionary<string, (Border Fill, TextBlock Value, FrameworkElement Row)> _progressBars = [];

    private void ShowRight()
    {
        if (_rendering) return;
        _rendering = true;
        try
        {
            ShowRightCore();
        }
        finally
        {
            _rendering = false;
        }
    }

    private void ShowRightCore()
    {
        ProgressTab.Foreground = B(_showScreen ? "TextTertiary" : "TextPrimary");
        ScreenTab.Foreground = B(_showScreen ? "TextPrimary" : "TextTertiary");
        RightPanel.Children.Clear();
        ScreenTotal.Text = "";
        if (_showScreen) ShowScreenTime();
        else
        {
            _progressBars.Clear();
            foreach (var (key, label) in new[] { ("day", "Hari"), ("week", "Minggu"), ("month", "Bulan"), ("year", "Tahun") })
                RightPanel.Children.Add(BarRow(key, label, _progressBars));
            // Lebar bar baru diketahui setelah tata letak.
            Dispatcher.BeginInvoke(ShowProgress, DispatcherPriority.Loaded);
        }
    }

    private void ShowProgress()
    {
        if (_progressBars.Count == 0) return;
        var p = DayProgress.At(DateTime.Now);
        var culture = CultureInfo.GetCultureInfo("id-ID");
        var now = DateTime.Now;
        SetProgress("day", p.Day, $"{DayProgress.Percent(p.Day)}%", $"Sisa {(now.Date.AddDays(1) - now):h\\ \\j\\ m\\ \\m} hari ini");
        SetProgress("week", p.Week, $"{DayProgress.Percent(p.Week)}%", now.ToString("dddd", culture));
        SetProgress("month", p.Month, $"{DayProgress.Percent(p.Month)}%", now.ToString("d MMMM", culture));
        SetProgress("year", p.Year, $"{DayProgress.Percent(p.Year)}%", $"Hari ke-{now.DayOfYear}");
    }

    private void SetProgress(string key, double fraction, string value, string tooltip)
    {
        var (fill, text, row) = _progressBars[key];
        fill.Width = Math.Clamp(fraction, 0, 1) * BarWidth(fill);
        text.Text = value;
        row.ToolTip = tooltip;
    }

    private void ShowScreenTime()
    {
        if (_screen is null) return;
        if (!_screen.Enabled)
        {
            RightPanel.Children.Add(new TextBlock
            {
                Text = "Lihat berapa lama tiap aplikasi kamu pakai hari ini. Hanya nama aplikasi yang dicatat, dan datanya tidak keluar dari laptop ini.",
                Style = (Style)FindResource("Peeko.Text.Caption"),
                TextWrapping = TextWrapping.Wrap,
            });
            var on = new Button
            {
                Content = "Nyalakan screen time",
                Style = (Style)FindResource("Peeko.Button.Pill"),
                Height = 26,
                Padding = new Thickness(12, 0, 12, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 10, 0, 0),
            };
            on.Click += (_, _) => _settings?.Update(s => s.ScreenTimeEnabled = true);
            RightPanel.Children.Add(on);
            return;
        }

        _screen.Flush();
        var today = _screen.Log.Day(DateTime.Today);
        var total = _screen.Log.Total(DateTime.Today);
        ScreenTotal.Text = ScreenTime.Format(total);
        if (today.Count == 0)
        {
            RightPanel.Children.Add(new TextBlock { Text = "Belum ada catatan hari ini.", Style = (Style)FindResource("Peeko.Text.Caption") });
            return;
        }
        double max = today[0].Time.TotalSeconds;
        var bars = new Dictionary<string, (Border, TextBlock, FrameworkElement)>();
        foreach (var app in today.Take(4))
        {
            var row = BarRow(app.App, app.App, bars, labelWidth: 100, valueWidth: 58);
            RightPanel.Children.Add(row);
            var (fill, text, _) = bars[app.App];
            text.Text = ScreenTime.Format(app.Time);
            double fraction = max <= 0 ? 0 : app.Time.TotalSeconds / max;
            fill.Loaded += (_, _) => fill.Width = fraction * BarWidth(fill);
        }
    }

    // ── Bagian ─────────────────────────────────────────────────────────────

    /// <summary>Satu baris: label · bar tipis · nilai.</summary>
    private FrameworkElement BarRow(string key, string label, Dictionary<string, (Border, TextBlock, FrameworkElement)> into, double labelWidth = 52, double valueWidth = 76)
    {
        var grid = new Grid { Height = 24, Background = Brushes.Transparent };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(valueWidth) });

        grid.Children.Add(new TextBlock
        {
            Text = label,
            Style = (Style)FindResource("Peeko.Text.Caption"),
            Foreground = B("TextSecondary"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 6, 0),
        });

        var track = new Grid { Height = 5, VerticalAlignment = VerticalAlignment.Center };
        track.Children.Add(new Border { CornerRadius = new CornerRadius(2.5), Background = B("SurfaceRaised") });
        var fill = new Border { CornerRadius = new CornerRadius(2.5), Background = B("Accent"), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
        track.Children.Add(fill);
        Grid.SetColumn(track, 1);
        grid.Children.Add(track);

        var value = new TextBlock
        {
            Style = (Style)FindResource("Peeko.Text.Caption"),
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "–",
        };
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);

        into[key] = (fill, value, grid);
        return grid;
    }

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
