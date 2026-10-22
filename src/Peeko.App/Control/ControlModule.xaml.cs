// Diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): panel Command Center.

using System.Windows;
using System.Windows.Controls;
using Peeko.Platform.Control;
using Peeko.Platform.Network;
using Windows.Devices.Radios;

namespace Peeko.App.Control;

/// <summary>
/// Pengaturan cepat. Status dibaca ulang setiap kali tab terlihat (tidak ada pemantauan di latar),
/// dan semua panggilan ke Windows berjalan di thread latar supaya panel tidak tersendat.
/// </summary>
public partial class ControlModule : UserControl
{
    private bool _loading;
    private bool _visible;
    private int _pendingBrightness = -1;
    private int _brightnessBusy;

    public ControlModule()
    {
        InitializeComponent();
    }

    /// <summary>Jangan Ganggu diubah: peek Peeko (kecuali baterai kritis) ditahan selama aktif.</summary>
    public event Action<bool>? QuietChanged;

    /// <summary>Volume/kecerahan sedang digeser dari panel ini; peek-nya tidak perlu muncul.</summary>
    public bool IsAdjusting => _visible;

    public bool Quiet
    {
        get => QuietTile.IsChecked == true;
        set
        {
            QuietTile.IsChecked = value;
            QuietStatus.Text = value ? "Nyala" : "Mati";
        }
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (visible) _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var wifiTask = SystemControls.GetRadioAsync(RadioKind.WiFi);
        var bluetoothTask = SystemControls.GetRadioAsync(RadioKind.Bluetooth);
        var snapshot = await Task.Run(() => (
            Ssid: WifiWatcher.ReadSsid(),
            Volume: SystemControls.GetVolume(),
            Brightness: SystemControls.GetBrightness(),
            Saver: SystemControls.IsEnergySaverOn()));
        var wifi = await wifiTask;
        var bluetooth = await bluetoothTask;

        _loading = true;
        ShowRadio(WifiTile, WifiStatus, wifi, wifi == true ? snapshot.Ssid ?? "Belum tersambung" : null);
        ShowRadio(BluetoothTile, BluetoothStatus, bluetooth, null);
        SaverTile.IsChecked = snapshot.Saver;
        SaverStatus.Text = snapshot.Saver ? "Nyala" : "Mati";

        VolumeSlider.IsEnabled = snapshot.Volume is not null;
        if (snapshot.Volume is { } v) VolumeSlider.Value = v.Muted ? 0 : Math.Round(v.Level * 100);
        VolumeText.Text = snapshot.Volume is null ? "–" : $"{VolumeSlider.Value:0}%";

        BrightnessSlider.IsEnabled = snapshot.Brightness is not null;
        if (snapshot.Brightness is { } b) BrightnessSlider.Value = b;
        BrightnessText.Text = snapshot.Brightness is null ? "–" : $"{BrightnessSlider.Value:0}%";
        _loading = false;
    }

    private static void ShowRadio(System.Windows.Controls.Primitives.ToggleButton tile, TextBlock status, bool? on, string? detail)
    {
        tile.IsEnabled = on is not null;
        tile.IsChecked = on == true;
        status.Text = on switch
        {
            null => "Tidak tersedia",
            true => detail ?? "Nyala",
            false => "Mati",
        };
    }

    private async void OnWifi(object sender, RoutedEventArgs e) => await ToggleRadio(RadioKind.WiFi, WifiTile, WifiStatus);

    private async void OnBluetooth(object sender, RoutedEventArgs e) => await ToggleRadio(RadioKind.Bluetooth, BluetoothTile, BluetoothStatus);

    private async Task ToggleRadio(RadioKind kind, System.Windows.Controls.Primitives.ToggleButton tile, TextBlock status)
    {
        bool want = tile.IsChecked == true;
        status.Text = want ? "Menyalakan…" : "Mematikan…";
        tile.IsEnabled = false;
        var result = await SystemControls.SetRadioAsync(kind, want);
        if (kind == RadioKind.WiFi && result == true)
        {
            // SSID baru muncul setelah tersambung ulang; tampilkan "Nyala" dulu.
            ShowRadio(tile, status, true, null);
            await Task.Delay(TimeSpan.FromSeconds(4));
            if (_visible) await RefreshAsync();
            return;
        }
        ShowRadio(tile, status, result ?? await SystemControls.GetRadioAsync(kind), null);
    }

    private void OnQuiet(object sender, RoutedEventArgs e)
    {
        Quiet = QuietTile.IsChecked == true;
        QuietChanged?.Invoke(Quiet);
    }

    private void OnSaver(object sender, RoutedEventArgs e)
    {
        // Windows tidak menyediakan API untuk menyalakannya; buka halamannya seperti Bloom.
        SaverTile.IsChecked = !SaverTile.IsChecked;
        SystemControls.OpenSettings("ms-settings:batterysaver");
    }

    private void OnNotifications(object sender, RoutedEventArgs e) => SystemControls.OpenActionCenter();

    private void OnSnip(object sender, RoutedEventArgs e) => SystemControls.Open("ms-screenclip:");

    private void OnLock(object sender, RoutedEventArgs e) => SystemControls.LockScreen();

    private void OnWindowsSettings(object sender, RoutedEventArgs e) => SystemControls.OpenSettings("ms-settings:");

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeText is null || _loading) return;
        VolumeText.Text = $"{e.NewValue:0}%";
        float level = (float)(e.NewValue / 100);
        _ = Task.Run(() => SystemControls.SetVolume(level));
    }

    private void OnBrightnessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BrightnessText is null || _loading) return;
        BrightnessText.Text = $"{e.NewValue:0}%";
        // WMI lambat (±100 ms); saat digeser cepat cukup kirim nilai terakhir.
        Volatile.Write(ref _pendingBrightness, (int)Math.Round(e.NewValue));
        if (Interlocked.Exchange(ref _brightnessBusy, 1) == 1) return;
        _ = Task.Run(() =>
        {
            int sent = -1;
            while (true)
            {
                int value = Volatile.Read(ref _pendingBrightness);
                if (value != sent)
                {
                    SystemControls.SetBrightness(value);
                    sent = value;
                    continue;
                }
                Volatile.Write(ref _brightnessBusy, 0);
                // Nilai baru datang tepat setelah dilepas: ambil lagi kalau belum ada yang mengambil.
                if (Volatile.Read(ref _pendingBrightness) == sent || Interlocked.Exchange(ref _brightnessBusy, 1) == 1) break;
            }
        });
    }
}
