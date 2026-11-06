// Diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): panel Command Center.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Peeko.Platform.Audio;
using Peeko.Platform.Control;
using Peeko.Platform.Network;
using Windows.Devices.Radios;

namespace Peeko.App.Control;

/// <summary>
/// Pengaturan cepat. Status dibaca ulang setiap kali tab terlihat (tidak ada pemantauan di latar),
/// dan semua panggilan ke Windows berjalan di thread latar supaya panel tidak tersendat.
/// Panah di ubin Wi-Fi/Bluetooth dan di samping volume membuka daftar jaringan/perangkat.
/// </summary>
public partial class ControlModule : UserControl
{
    private bool _loading;
    private bool _visible;
    private int _pendingBrightness = -1;
    private int _brightnessBusy;
    private string? _detail;
    private int _detailVersion;
    private string? _passwordSsid;
    private readonly WifiNetworks _wifiNetworks = new();

    public ControlModule()
    {
        InitializeComponent();
    }

    /// <summary>Jangan Ganggu diubah: peek Peeko (kecuali baterai kritis) ditahan selama aktif.</summary>
    public event Action<bool>? QuietChanged;

    /// <summary>Mengetik password Wi-Fi: pemilik mengaktifkan jendela dan menahan panel terbuka.</summary>
    public event Action<bool>? TextInputChanged;

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
        else CloseDetail();
    }

    private async Task RefreshAsync()
    {
        var wifiTask = SystemControls.GetRadioAsync(RadioKind.WiFi);
        var bluetoothTask = SystemControls.GetRadioAsync(RadioKind.Bluetooth);
        var snapshot = await Task.Run(() => (
            Ssid: WifiWatcher.ReadSsid(),
            Volume: SystemControls.GetVolume(),
            Brightness: SystemControls.GetBrightness(),
            Saver: SystemControls.IsEnergySaverOn(),
            Dark: SystemControls.IsDarkMode(),
            Mic: AudioDevices.GetMicMuted()));
        var wifi = await wifiTask;
        var bluetooth = await bluetoothTask;

        _loading = true;
        ShowRadio(WifiTile, WifiStatus, wifi, wifi == true ? snapshot.Ssid ?? "Belum tersambung" : null);
        ShowRadio(BluetoothTile, BluetoothStatus, bluetooth, null);
        SaverTile.IsChecked = snapshot.Saver;
        SaverStatus.Text = snapshot.Saver ? "Nyala" : "Mati";
        DarkButton.IsEnabled = snapshot.Dark is not null;
        DarkButton.IsChecked = snapshot.Dark == true;
        MicButton.IsEnabled = snapshot.Mic is not null;
        SetMicMuted(snapshot.Mic == true);

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
            if (_visible && _detail is null) await RefreshAsync();
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

    // ── Tombol cepat ───────────────────────────────────────────────────────

    private void OnAwake(object sender, RoutedEventArgs e)
    {
        bool on = AwakeButton.IsChecked == true;
        SystemControls.SetKeepAwake(on);
        AwakeButton.ToolTip = on ? "Tetap terjaga: aktif, layar tidak mati sampai dimatikan" : "Tetap terjaga: layar tidak mati selama aktif";
    }

    private void OnDark(object sender, RoutedEventArgs e)
    {
        bool dark = DarkButton.IsChecked == true;
        _ = Task.Run(() => SystemControls.SetDarkMode(dark));
    }

    private void OnMic(object sender, RoutedEventArgs e)
    {
        bool mute = MicButton.IsChecked == true;
        SetMicMuted(mute);
        _ = Task.Run(() => AudioDevices.SetMicMuted(mute));
    }

    /// <summary>Tampilkan status bisu mikrofon (juga saat dibisukan dari luar Peeko).</summary>
    public void SetMicMuted(bool muted)
    {
        MicButton.IsChecked = muted;
        MicButton.ToolTip = muted ? "Mikrofon dibisukan · klik untuk menyalakan" : "Bisukan mikrofon";
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

    // ── Daftar Wi-Fi / Bluetooth / audio ───────────────────────────────────

    private void OnOpenDetail(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // jangan ikut menyalakan/mematikan ubin di bawahnya
        if (sender is FrameworkElement { Tag: string kind }) OpenDetail(kind);
    }

    private void OnCloseDetail(object sender, RoutedEventArgs e) => CloseDetail();

    private void OnRefreshDetail(object sender, RoutedEventArgs e)
    {
        if (_detail is { } kind) _ = LoadDetailAsync(kind);
    }

    /// <summary>Buka daftar: "Wifi", "Bluetooth", atau "Audio".</summary>
    public void OpenDetail(string kind)
    {
        _detail = kind;
        DetailTitle.Text = kind switch { "Wifi" => "Wi-Fi", "Bluetooth" => "Bluetooth", _ => "Perangkat audio" };
        DetailList.Children.Clear();
        MainView.Visibility = Visibility.Collapsed;
        DetailView.Visibility = Visibility.Visible;
        HidePassword();
        _ = LoadDetailAsync(kind);
    }

    private void CloseDetail()
    {
        if (_detail is null) return;
        _detail = null;
        _detailVersion++;
        HidePassword();
        DetailView.Visibility = Visibility.Collapsed;
        MainView.Visibility = Visibility.Visible;
        if (_visible) _ = RefreshAsync();
    }

    private async Task LoadDetailAsync(string kind)
    {
        int version = ++_detailVersion;
        DetailHint.Text = kind == "Wifi" ? "Memindai…" : "Memuat…";
        DetailRefresh.IsEnabled = false;
        try
        {
            switch (kind)
            {
                case "Wifi":
                    var networks = await _wifiNetworks.ScanAsync();
                    if (version != _detailVersion) return;
                    ShowWifi(networks);
                    break;
                case "Bluetooth":
                    var devices = await Task.Run(BluetoothDevices.ListAsync);
                    if (version != _detailVersion) return;
                    ShowBluetooth(devices);
                    break;
                default:
                    var audio = await Task.Run(() => (Out: AudioDevices.List(false), In: AudioDevices.List(true)));
                    if (version != _detailVersion) return;
                    ShowAudio(audio.Out, audio.In);
                    break;
            }
        }
        finally
        {
            if (version == _detailVersion) DetailRefresh.IsEnabled = true;
        }
    }

    private void ShowWifi(IReadOnlyList<WifiNetwork> networks)
    {
        DetailList.Children.Clear();
        DetailHint.Text = networks.Count == 0 ? "" : $"{networks.Count} jaringan";
        if (networks.Count == 0)
        {
            AddEmpty(WifiTile.IsChecked == true ? "Tidak ada jaringan, atau Windows menolak pemindaian." : "Wi-Fi mati. Nyalakan dulu dari ubin Wi-Fi.");
            return;
        }
        foreach (var n in networks)
        {
            var row = AddRow(Icon("Peeko.Icon.Wifi"), n.Ssid, "", n.Connected,
                subtitle: n.Connected ? "Tersambung · klik untuk memutus" : null, bars: n.Bars, locked: n.Secured);
            var network = n;
            row.MouseLeftButtonUp += async (_, _) =>
            {
                if (network.Connected)
                {
                    DetailHint.Text = "Memutus…";
                    _wifiNetworks.Disconnect();
                    await Task.Delay(1500);
                    if (_detail == "Wifi") await LoadDetailAsync("Wifi");
                    return;
                }
                await ConnectWifiAsync(network.Ssid, null);
            };
        }
    }

    private async Task ConnectWifiAsync(string ssid, string? password)
    {
        DetailHint.Text = "Menyambung…";
        var result = await _wifiNetworks.ConnectAsync(ssid, password);
        if (_detail != "Wifi") return;
        switch (result)
        {
            case WifiConnectResult.Connected:
                HidePassword();
                await LoadDetailAsync("Wifi");
                break;
            case WifiConnectResult.NeedsPassword:
                ShowPassword(ssid, $"Password {ssid}");
                DetailHint.Text = "";
                break;
            case WifiConnectResult.WrongPassword:
                ShowPassword(ssid, "Password salah, coba lagi");
                DetailHint.Text = "";
                break;
            default:
                DetailHint.Text = "Gagal menyambung";
                break;
        }
    }

    private void ShowPassword(string ssid, string placeholder)
    {
        _passwordSsid = ssid;
        PasswordInput.Password = "";
        PasswordPlaceholder.Text = placeholder;
        PasswordPlaceholder.Visibility = Visibility.Visible;
        bool wasHidden = PasswordRow.Visibility != Visibility.Visible;
        PasswordRow.Visibility = Visibility.Visible;
        if (wasHidden) TextInputChanged?.Invoke(true);
        Dispatcher.BeginInvoke(() => { PasswordInput.Focus(); Keyboard.Focus(PasswordInput); }, DispatcherPriority.Input);
    }

    private void HidePassword()
    {
        if (PasswordRow.Visibility != Visibility.Visible) return;
        PasswordInput.Password = "";
        PasswordRow.Visibility = Visibility.Collapsed;
        _passwordSsid = null;
        TextInputChanged?.Invoke(false);
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e) =>
        PasswordPlaceholder.Visibility = PasswordInput.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnPasswordKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnPasswordConnect(sender, e);
        else if (e.Key == Key.Escape) HidePassword();
        else return;
        e.Handled = true;
    }

    private async void OnPasswordConnect(object sender, RoutedEventArgs e)
    {
        if (_passwordSsid is not { } ssid || PasswordInput.Password.Length == 0) return;
        string password = PasswordInput.Password;
        PasswordInput.Password = "";
        await ConnectWifiAsync(ssid, password);
    }

    private void ShowBluetooth(IReadOnlyList<BluetoothItem> devices)
    {
        DetailList.Children.Clear();
        DetailHint.Text = "";
        if (devices.Count == 0)
        {
            AddEmpty(BluetoothTile.IsChecked == false
                ? "Bluetooth mati. Nyalakan dulu dari ubin Bluetooth."
                : "Belum ada perangkat yang dipasangkan.");
        }
        foreach (var d in devices)
        {
            string sub = (d.Connected, d.CanToggle) switch
            {
                (true, true) => "Tersambung · klik untuk memutus",
                (true, false) => "Tersambung",
                (false, true) => "Klik untuk menyambung",
                _ => "Tidak tersambung",
            };
            var row = AddRow(Icon(d.Kind == BluetoothKind.Audio ? "Peeko.Icon.Volume" : "Peeko.Icon.Bluetooth"), d.Name,
                d.Battery is { } b ? $"{b}%" : "", d.Connected, subtitle: sub);
            var device = d;
            row.MouseLeftButtonUp += async (_, _) =>
            {
                if (!device.CanToggle)
                {
                    // Selain perangkat audio, Windows hanya bisa menyambung dari halaman Pengaturan.
                    SystemControls.OpenSettings("ms-settings:bluetooth");
                    return;
                }
                DetailHint.Text = device.Connected ? "Memutus…" : "Menyambung…";
                bool sent = await Task.Run(() => BluetoothDevices.SetAudioConnected(device.Address, !device.Connected));
                if (!sent)
                {
                    DetailHint.Text = "Gagal";
                    return;
                }
                // Driver butuh beberapa detik untuk benar-benar tersambung/terputus.
                await Task.Delay(TimeSpan.FromSeconds(device.Connected ? 2 : 5));
                if (_detail == "Bluetooth") await LoadDetailAsync("Bluetooth");
            };
        }
        AddLink("Pasangkan perangkat baru…", () => SystemControls.OpenSettings("ms-settings:bluetooth"));
    }

    private void ShowAudio(IReadOnlyList<AudioDevice> outputs, IReadOnlyList<AudioDevice> inputs)
    {
        DetailList.Children.Clear();
        DetailHint.Text = "";
        AddHeader("SPEAKER");
        if (outputs.Count == 0) AddEmpty("Tidak ada speaker");
        foreach (var d in outputs) AddAudioRow(d, "Peeko.Icon.Volume");
        AddHeader("MIKROFON");
        if (inputs.Count == 0) AddEmpty("Tidak ada mikrofon");
        foreach (var d in inputs) AddAudioRow(d, "Peeko.Icon.Mic");
    }

    private void AddAudioRow(AudioDevice d, string icon)
    {
        // "Speaker/HP (Realtek High Definition Audio)" → judul "Speaker/HP", keterangan nama driver.
        int paren = d.Name.IndexOf(" (", StringComparison.Ordinal);
        string title = paren > 0 && d.Name.EndsWith(')') ? d.Name[..paren] : d.Name;
        string? driver = paren > 0 && d.Name.EndsWith(')') ? d.Name[(paren + 2)..^1] : null;
        var row = AddRow(Icon(icon), title, "", d.IsDefault, subtitle: driver, check: d.IsDefault);
        row.MouseLeftButtonUp += async (_, _) =>
        {
            if (d.IsDefault) return;
            await Task.Run(() => AudioDevices.SetDefault(d.Id));
            if (_detail == "Audio") await LoadDetailAsync("Audio");
        };
    }

    // ── Bagian daftar ──────────────────────────────────────────────────────

    private Border AddRow(Geometry icon, string title, string right, bool active, string? subtitle = null, int bars = -1,
        bool locked = false, bool check = false)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new System.Windows.Shapes.Path
        {
            Style = (Style)FindResource("Peeko.IconPath"),
            Data = icon,
            Stroke = B(active ? "Accent" : "TextSecondary"),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            LayoutTransform = new ScaleTransform(0.7, 0.7),
        });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = title,
            Style = (Style)FindResource(active ? "Peeko.Text.BodyStrong" : "Peeko.Text.Body"),
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (subtitle is not null)
            text.Children.Add(new TextBlock { Text = subtitle, Style = (Style)FindResource("Peeko.Text.Caption"), FontSize = 10.5 });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var tail = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (right.Length > 0)
            tail.Children.Add(new TextBlock
            {
                Text = right,
                Style = (Style)FindResource("Peeko.Text.Caption"),
                Foreground = B(active ? "Accent" : "TextSecondary"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            });
        if (locked) tail.Children.Add(SmallIcon("Peeko.Icon.Lock", "TextTertiary"));
        if (bars >= 0) tail.Children.Add(SignalBars(bars, active));
        if (check) tail.Children.Add(SmallIcon("Peeko.Icon.Check", "Accent"));
        Grid.SetColumn(tail, 2);
        grid.Children.Add(tail);

        var row = new Border
        {
            Height = subtitle is null ? 32 : 38,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 0, 10, 0),
            Margin = new Thickness(0, 0, 0, 2),
            Background = active ? B("SurfaceRaised") : Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = grid,
        };
        row.MouseEnter += (_, _) => row.Background = B("SurfaceHover");
        row.MouseLeave += (_, _) => row.Background = active ? B("SurfaceRaised") : Brushes.Transparent;
        DetailList.Children.Add(row);
        return row;
    }

    private FrameworkElement SmallIcon(string key, string brush) => new System.Windows.Shapes.Path
    {
        Style = (Style)FindResource("Peeko.IconPath"),
        Data = Icon(key),
        Stroke = B(brush),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(8, 0, 0, 0),
        LayoutTransform = new ScaleTransform(0.55, 0.55),
    };

    /// <summary>Empat batang sinyal, yang terisi sesuai kekuatan.</summary>
    private static FrameworkElement SignalBars(int bars, bool active)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        for (int i = 0; i < 4; i++)
        {
            panel.Children.Add(new Border
            {
                Width = 3,
                Height = 4 + i * 2.5,
                Margin = new Thickness(i == 0 ? 0 : 1.5, 0, 0, 0),
                CornerRadius = new CornerRadius(1),
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = B(i < bars ? (active ? "Accent" : "TextSecondary") : "SurfacePressed"),
            });
        }
        return panel;
    }

    private void AddHeader(string text) => DetailList.Children.Add(new TextBlock
    {
        Text = text,
        Style = (Style)FindResource("Peeko.Text.Overline"),
        Margin = new Thickness(8, DetailList.Children.Count == 0 ? 0 : 8, 0, 4),
    });

    private void AddEmpty(string text) => DetailList.Children.Add(new TextBlock
    {
        Text = text,
        Style = (Style)FindResource("Peeko.Text.Caption"),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(8, 4, 8, 6),
    });

    private void AddLink(string text, Action click)
    {
        var link = new TextBlock
        {
            Text = text,
            Style = (Style)FindResource("Peeko.Text.Caption"),
            Foreground = B("Accent"),
            Cursor = Cursors.Hand,
            Margin = new Thickness(8, 6, 0, 0),
        };
        link.MouseLeftButtonUp += (_, _) => click();
        DetailList.Children.Add(link);
    }

    private static Geometry Icon(string key) => (Geometry)Application.Current.Resources[key];

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
