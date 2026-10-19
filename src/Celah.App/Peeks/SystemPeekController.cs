using System.Windows.Threading;
using Celah.Core.Peeks;
using Celah.Platform.Audio;
using Celah.Platform.Display;
using Celah.Platform.Input;
using Celah.Platform.Network;
using Celah.Platform.Power;
using Microsoft.Extensions.Logging;

namespace Celah.App.Peeks;

/// <summary>
/// Mengubah event sistem (volume, kecerahan, Caps Lock, bahasa, baterai, Wi-Fi, perangkat audio)
/// menjadi <see cref="PeekRequest"/> di thread UI. Semua sumber berbasis event, tanpa polling.
/// </summary>
public sealed class SystemPeekController : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<SystemPeekController> _log;

    private readonly AudioDeviceWatcher _audio = new();
    private readonly BrightnessWatcher _brightness = new();
    private readonly KeyboardWatcher _keyboard = new();
    private readonly PowerWatcher _power = new();
    private readonly WifiWatcher _wifi = new();

    private readonly BatteryPeekLogic _battery = new();
    private readonly WifiPeekLogic _wifiLogic = new();

    private int _sample;

    public SystemPeekController(Dispatcher dispatcher, ILogger<SystemPeekController> log)
    {
        _dispatcher = dispatcher;
        _log = log;
    }

    public event Action<PeekRequest>? PeekRequested;

    public async Task StartAsync()
    {
        _audio.VolumeChanged += (level, muted) => Raise(new PeekRequest(PeekKind.Volume) { Level = level, Flag = muted });
        _audio.DeviceConnected += (name, headphone) => Raise(new PeekRequest(PeekKind.AudioDeviceConnected) { Text = name, Flag = headphone });
        _audio.DeviceDisconnected += name => Raise(new PeekRequest(PeekKind.AudioDeviceDisconnected)
        {
            Text = name,
            Flag = name.Contains("head", StringComparison.OrdinalIgnoreCase) || name.Contains("buds", StringComparison.OrdinalIgnoreCase),
        });
        _brightness.Changed += value => Raise(new PeekRequest(PeekKind.Brightness) { Level = value / 100.0 });
        _keyboard.CapsLockChanged += on => Raise(new PeekRequest(PeekKind.CapsLock) { Flag = on });
        _keyboard.LanguageChanged += (lcid, previous) =>
        {
            var (code, name) = KeyboardLanguage.FromLcid(lcid);
            var (oldCode, _) = KeyboardLanguage.FromLcid(previous);
            Raise(new PeekRequest(PeekKind.Language) { Text = code, Detail = oldCode, Caption = name });
        };
        _power.StatusChanged += (onAc, percent, remaining) =>
            _dispatcher.BeginInvoke(() =>
            {
                if (_battery.Update(onAc, percent, remaining) is { } peek) PeekRequested?.Invoke(peek);
            });
        _wifi.SsidChanged += ssid =>
            _dispatcher.BeginInvoke(() =>
            {
                if (_wifiLogic.Update(ssid) is { } peek) PeekRequested?.Invoke(peek);
            });

        Try("kecerahan", () =>
        {
            if (!_brightness.Start()) _log.LogInformation("Kecerahan via WMI tidak didukung di perangkat ini");
        });
        Try("keyboard", _keyboard.Start);
        Try("daya", _power.Start);
        Try("Wi-Fi", _wifi.Start);
        try
        {
            await _audio.StartAsync();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Sumber peek audio tidak bisa dimulai");
        }
    }

    public void SetBatteryThreshold(int percent) => _battery.LowThreshold = percent;

    /// <summary>Jendela aktif berganti: catat bahasanya tanpa peek (bahasa bisa per-aplikasi).</summary>
    public void OnForegroundChanged() => _keyboard.SyncLanguageSilently();

    /// <summary>Contoh untuk menu tray "Uji peek": tiap klik menampilkan varian berikutnya.</summary>
    public void RaiseSample()
    {
        PeekRequest[] samples =
        [
            new(PeekKind.Volume) { Level = 0.64 },
            new(PeekKind.Volume) { Level = 0.64, Flag = true },
            new(PeekKind.Brightness) { Level = 0.8 },
            new(PeekKind.CapsLock) { Flag = true },
            new(PeekKind.CapsLock) { Flag = false },
            new(PeekKind.Language) { Text = "ID", Detail = "EN", Caption = "Bahasa Indonesia" },
            new(PeekKind.BatteryLow) { Percent = 18 },
            new(PeekKind.BatteryCritical) { Percent = 8 },
            new(PeekKind.Charging) { Percent = 47 },
            new(PeekKind.Discharging) { Percent = 47, Detail = "±3 j 20 m" },
            new(PeekKind.WifiConnected) { Text = "Rumah_5G" },
            new(PeekKind.WifiDisconnected) { Text = "Rumah_5G" },
            new(PeekKind.AudioDeviceConnected) { Text = "Headset Kerja", Flag = true },
            new(PeekKind.AudioDeviceDisconnected) { Text = "Speaker Ruang Tamu" },
        ];
        PeekRequested?.Invoke(samples[_sample++ % samples.Length]);
    }

    private void Raise(PeekRequest request) => _dispatcher.BeginInvoke(() => PeekRequested?.Invoke(request));

    private void Try(string name, Action start)
    {
        try
        {
            start();
        }
        catch (Exception ex)
        {
            // Satu sumber gagal (mis. driver audio aneh) tidak boleh mematikan yang lain.
            _log.LogWarning(ex, "Sumber peek {Name} tidak bisa dimulai", name);
        }
    }

    public void Dispose()
    {
        _audio.Dispose();
        _brightness.Dispose();
        _keyboard.Dispose();
        _power.Dispose();
        _wifi.Dispose();
    }
}
