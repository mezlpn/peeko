// Diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): src/hooks/useWeather.ts.

using System.Net.Http;
using Microsoft.Extensions.Logging;
using Peeko.App.Settings;
using Peeko.App.Updates;
using Peeko.Core.Status;

namespace Peeko.App.Today;

/// <summary>
/// Cuaca dari Open-Meteo. Tidak ada pengambilan di latar: data hanya diambil saat tab Hari ini dibuka
/// dan data terakhir sudah lebih dari 30 menit. Lokasi: kota pilihan pengguna, atau sekali dari
/// perkiraan IP (ipapi.co), lalu disimpan di pengaturan.
/// </summary>
public sealed class WeatherService
{
    private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(30);

    private readonly HttpClient _http;
    private readonly SettingsService _settings;
    private readonly ILogger<WeatherService> _log;
    private DateTime _fetchedAt = DateTime.MinValue;
    private Task<WeatherNow?>? _inFlight;

    public WeatherService(SettingsService settings, ILogger<WeatherService> log)
    {
        _settings = settings;
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Peeko/{UpdateController.CurrentVersion.ToString(3)} (aplikasi notch Windows)");
    }

    public WeatherNow? Latest { get; private set; }

    public string? Place => _settings.Current.WeatherPlace;

    public bool IsEnabled => _settings.Current.WeatherEnabled;

    /// <summary>Data terbaru (dari cache bila masih segar), atau null jika dimatikan/gagal.</summary>
    public Task<WeatherNow?> GetAsync()
    {
        if (!_settings.Current.WeatherEnabled) return Task.FromResult<WeatherNow?>(null);
        if (Latest is not null && DateTime.UtcNow - _fetchedAt < Fresh) return Task.FromResult<WeatherNow?>(Latest);
        return _inFlight ??= FetchAsync();
    }

    /// <summary>Kota diganti di Pengaturan: cari koordinatnya. Mengembalikan nama tempat yang ditemukan.</summary>
    public async Task<string?> SetCityAsync(string? city)
    {
        GeoPoint? at = null;
        if (!string.IsNullOrWhiteSpace(city))
        {
            at = await GetJsonAsync(Weather.GeocodeUrl(city), Weather.ParseGeocode);
            if (at is null) return null;
        }
        _settings.Update(s =>
        {
            s.WeatherCity = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
            s.WeatherLatitude = at?.Latitude;
            s.WeatherLongitude = at?.Longitude;
            s.WeatherPlace = at?.City;
        });
        Latest = null;
        return at?.City ?? "";
    }

    private async Task<WeatherNow?> FetchAsync()
    {
        try
        {
            var at = await ResolveLocationAsync();
            if (at is null) return Latest;
            var now = await GetJsonAsync(Weather.ForecastUrl(at), Weather.ParseForecast);
            if (now is not null)
            {
                Latest = now;
                _fetchedAt = DateTime.UtcNow;
            }
            return Latest;
        }
        finally
        {
            _inFlight = null;
        }
    }

    private async Task<GeoPoint?> ResolveLocationAsync()
    {
        var s = _settings.Current;
        if (s.WeatherLatitude is { } lat && s.WeatherLongitude is { } lon) return new GeoPoint(lat, lon, s.WeatherPlace);

        var at = string.IsNullOrWhiteSpace(s.WeatherCity)
            ? await GetJsonAsync(Weather.IpLocationUrl, Weather.ParseIpLocation)
            : await GetJsonAsync(Weather.GeocodeUrl(s.WeatherCity), Weather.ParseGeocode);
        if (at is null) return null;
        _settings.Update(x =>
        {
            x.WeatherLatitude = at.Latitude;
            x.WeatherLongitude = at.Longitude;
            x.WeatherPlace = at.City;
        });
        return at;
    }

    private async Task<T?> GetJsonAsync<T>(string url, Func<string, T?> parse) where T : class
    {
        try
        {
            return parse(await _http.GetStringAsync(url));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogDebug("Cuaca: {Host} gagal ({Message})", new Uri(url).Host, ex.Message);
            return null;
        }
    }
}
