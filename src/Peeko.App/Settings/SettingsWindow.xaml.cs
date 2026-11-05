using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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

    private async void OnCityKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        e.Handled = true;
        await _vm.SaveCityAsync();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
