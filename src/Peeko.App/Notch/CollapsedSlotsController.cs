using System.Globalization;
using System.Windows.Threading;
using Microsoft.Win32;
using Peeko.App.Settings;
using Peeko.Core.Notch;
using Peeko.Platform.Control;

namespace Peeko.App.Notch;

/// <summary>
/// Mengisi info kiri/kanan notch tertutup. Diperbarui saat ada perubahan (salat, cuaca, daya,
/// pengaturan) dan tiap pergantian menit, tapi tick menit hanya hidup selama pil benar-benar terlihat.
/// </summary>
public sealed class CollapsedSlotsController : IDisposable
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    private readonly NotchWindow _window;
    private readonly SettingsService _settings;
    private readonly Prayer.PrayerController _prayer;
    private readonly Today.WeatherService _weather;
    private readonly DispatcherTimer _minute = new(DispatcherPriority.Background);
    private bool _visible;

    /// <summary>Teks tugas berikutnya (diisi modul tugas), null jika tidak ada.</summary>
    public Func<string?>? NextTask { get; set; }

    public CollapsedSlotsController(NotchWindow window, SettingsService settings, Prayer.PrayerController prayer, Today.WeatherService weather)
    {
        _window = window;
        _settings = settings;
        _prayer = prayer;
        _weather = weather;
        _minute.Tick += (_, _) =>
        {
            _minute.Interval = UntilNextMinute();
            Refresh();
        };
        prayer.Updated += Refresh;
        SystemEvents.PowerModeChanged += OnPower;
    }

    /// <summary>Pil tertutup sedang terlihat (bukan terbuka, garis tipis, atau tersembunyi).</summary>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (visible) Refresh();
        UpdateTick();
    }

    public void Refresh()
    {
        var s = _settings.Current;
        _window.SetSlots(Text(s.CollapsedLeft), Text(s.CollapsedRight));
        if (_visible && (s.CollapsedLeft == CollapsedSlot.Weather || s.CollapsedRight == CollapsedSlot.Weather)) _ = RefreshWeatherAsync();
        UpdateTick();
    }

    private string? Text(CollapsedSlot slot)
    {
        var now = DateTime.Now;
        switch (slot)
        {
            case CollapsedSlot.Clock:
                return now.ToString("HH:mm");
            case CollapsedSlot.Date:
                return now.ToString("ddd d MMM", Indonesian);
            case CollapsedSlot.Battery:
                return SystemControls.GetBattery() is { } b ? (b.Charging ? $"⚡{b.Percent}%" : $"{b.Percent}%") : null;
            case CollapsedSlot.Weather:
                return _weather.IsEnabled && _weather.Latest is { } w ? $"{w.Temperature}°" : null;
            case CollapsedSlot.NextPrayer:
                return _prayer.IsEnabled && _prayer.Next() is { } p ? $"{p.Label} {p.At:HH.mm}" : null;
            case CollapsedSlot.NextTask:
                return NextTask?.Invoke();
            case CollapsedSlot.DayProgress:
                return $"{Core.Status.DayProgress.Percent(Core.Status.DayProgress.At(now).Day)}% hari";
            default:
                return null;
        }
    }

    /// <summary>Cuaca diambil paling sering tiap 30 menit (cache WeatherService), hanya saat pilnya terlihat.</summary>
    private async Task RefreshWeatherAsync()
    {
        var before = _weather.Latest;
        await _weather.GetAsync();
        if (!ReferenceEquals(before, _weather.Latest))
        {
            var s = _settings.Current;
            _window.SetSlots(Text(s.CollapsedLeft), Text(s.CollapsedRight));
        }
    }

    private void UpdateTick()
    {
        var s = _settings.Current;
        bool tick = _visible && (CollapsedSlots.IsTimeBased(s.CollapsedLeft) || CollapsedSlots.IsTimeBased(s.CollapsedRight)
            || s.CollapsedLeft == CollapsedSlot.Weather || s.CollapsedRight == CollapsedSlot.Weather);
        if (tick && !_minute.IsEnabled)
        {
            _minute.Interval = UntilNextMinute();
            _minute.Start();
        }
        else if (!tick) _minute.Stop();
    }

    private static TimeSpan UntilNextMinute()
    {
        var now = DateTime.Now;
        return TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond) + TimeSpan.FromMilliseconds(50);
    }

    // Charger dicolok/dilepas: baterai berubah tanpa menunggu menit berikutnya.
    private void OnPower(object sender, PowerModeChangedEventArgs e) =>
        System.Windows.Application.Current.Dispatcher.BeginInvoke(Refresh);

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPower;
        _minute.Stop();
    }
}
