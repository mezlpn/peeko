using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using Peeko.Core.Settings;

namespace Peeko.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Scroll.SizeChanged += (_, _) => UpdateColumns();
        BuildSwatches();
    }

    /// <summary>Bulatan warna aksen; yang terpilih bercincin.</summary>
    private void BuildSwatches()
    {
        AccentSwatches.Children.Clear();
        var current = ThemePalette.ParseOrDefault(_vm.AccentColor);
        foreach (var (name, hex) in ThemePalette.Presets)
        {
            var c = ThemePalette.ParseOrDefault(hex);
            var dot = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)),
                BorderBrush = c == current ? Brushes.White : Brushes.Transparent,
                BorderThickness = new Thickness(2.5),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = name,
            };
            dot.MouseLeftButtonUp += (_, _) =>
            {
                _vm.AccentColor = hex == ThemePalette.Presets[0].Hex ? null : hex;
                BuildSwatches();
            };
            AccentSwatches.Children.Add(dot);
        }
    }

    private void OnNotchStyle(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { Style = (Style)FindResource("Peeko.TrayMenu"), PlacementTarget = (UIElement)sender };
        foreach (var (style, label) in new[] { (NotchStyle.Black, "Hitam"), (NotchStyle.Glass, "Kaca"), (NotchStyle.Tinted, "Berona aksen") })
        {
            var item = new MenuItem { Header = label, Style = (Style)FindResource("Peeko.TrayItem"), IsChecked = _vm.NotchStyle == style };
            item.Click += (_, _) => _vm.NotchStyle = style;
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    /// <summary>Dua kolom kartu jika cukup lebar (≥ 640 px area isi), satu kolom jika jendela sempit.</summary>
    private void UpdateColumns()
    {
        double available = Scroll.ActualWidth - 32 - SystemParameters.VerticalScrollBarWidth;
        ColumnProbe.Width = available >= 640 ? available / 2 : available;
    }

    /// <summary>Navigasi kiri sebagai anchor ke bagian yang sama di halaman.</summary>
    private void OnNav(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string name } && FindName(name) is FrameworkElement section && IsLoaded)
        {
            var offset = section.TranslatePoint(new Point(0, 0), Sections).Y;
            Scroll.ScrollToVerticalOffset(offset);
        }
    }

    private void OnSlimMode(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { Style = (Style)FindResource("Peeko.TrayMenu"), PlacementTarget = (UIElement)sender };
        foreach (var (mode, label) in new[] { (SlimMode.Auto, "Otomatis"), (SlimMode.AlwaysPill, "Selalu pil"), (SlimMode.AlwaysLine, "Selalu garis") })
        {
            var item = new MenuItem { Header = label, Style = (Style)FindResource("Peeko.TrayItem"), IsChecked = _vm.SlimMode == mode };
            item.Click += (_, _) => _vm.SlimMode = mode;
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    /// <summary>Pilih isi kiri/kanan notch tertutup.</summary>
    private void OnSlot(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        bool left = (string)button.Tag == "Left";
        var current = left ? _vm.CollapsedLeft : _vm.CollapsedRight;
        var menu = new ContextMenu { Style = (Style)FindResource("Peeko.TrayMenu"), PlacementTarget = button };
        foreach (var slot in Enum.GetValues<Core.Notch.CollapsedSlot>())
        {
            var item = new MenuItem { Header = Core.Notch.CollapsedSlots.Label(slot), Style = (Style)FindResource("Peeko.TrayItem"), IsChecked = slot == current };
            item.Click += (_, _) =>
            {
                if (left) _vm.CollapsedLeft = slot;
                else _vm.CollapsedRight = slot;
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void OnLink(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void OnFilee(object sender, RoutedEventArgs e) => _vm.ToggleFilee();

    private void OnPickAdhan(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pilih rekaman azan",
            Filter = "Audio (*.mp3;*.m4a;*.wav;*.wma;*.aac)|*.mp3;*.m4a;*.wav;*.wma;*.aac|Semua file|*.*",
        };
        if (dialog.ShowDialog(this) == true) _vm.SetAdhanFile(dialog.FileName);
    }

    private void OnAdhanPreview(object sender, RoutedEventArgs e) => _vm.ToggleAdhanPreview();

    private void OnEngines(object sender, RoutedEventArgs e) => _vm.OpenEngines();

    private async void OnSaveCity(object sender, RoutedEventArgs e) => await _vm.SaveCityAsync();

    private void OnSaveCalendar(object sender, RoutedEventArgs e) => _vm.SaveCalendar();

    private void OnInstallClaudeHooks(object sender, RoutedEventArgs e) => ClaudeHooks(install: true);

    private void OnRemoveClaudeHooks(object sender, RoutedEventArgs e) => ClaudeHooks(install: false);

    private void ClaudeHooks(bool install)
    {
        var service = ((App)Application.Current).Services.GetService(typeof(Sistem.ClaudeCodeService)) as Sistem.ClaudeCodeService;
        if (service is null) return;
        try
        {
            if (install)
            {
                if (!_vm.ClaudeCodeEnabled) _vm.ClaudeCodeEnabled = true;
                service.InstallHooks();
            }
            else
            {
                service.UninstallHooks();
            }
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException or System.IO.InvalidDataException)
        {
            MessageBox.Show(this, "settings.json Claude Code tidak bisa diubah: " + ex.Message, "Peeko", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        _vm.RefreshClaudeStatus();
    }

    private void OnBackup(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Simpan cadangan Peeko",
            FileName = BackupService.DefaultName,
            Filter = "Cadangan Peeko (*.zip)|*.zip",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            int n = BackupService.Create(dialog.FileName);
            BackupStatus.Text = $"{n} file dicadangkan ke {System.IO.Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            BackupStatus.Text = "Gagal menyimpan cadangan: " + ex.Message;
        }
    }

    private void OnRestore(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pilih cadangan Peeko",
            Filter = "Cadangan Peeko (*.zip)|*.zip",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != true) return;
        if (!BackupService.IsValid(dialog.FileName, out int files))
        {
            BackupStatus.Text = "File itu bukan cadangan Peeko.";
            return;
        }
        var answer = MessageBox.Show(this,
            $"Pulihkan {files} file dari cadangan ini? Data sekarang diganti, tapi salinannya tetap disimpan di folder \"sebelum-pulih\". Peeko akan dimulai ulang.",
            "Pulihkan cadangan", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.OK) BackupService.RestoreAndRestart(dialog.FileName);
    }

    private async void OnCityKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        e.Handled = true;
        await _vm.SaveCityAsync();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
