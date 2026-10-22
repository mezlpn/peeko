// Diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): src/hooks/useWeather.ts
// (Open-Meteo, kode cuaca WMO, lokasi dari pengaturan lalu dari IP).

using System.Globalization;
using System.Text.Json;

namespace Peeko.Core.Status;

public enum WeatherIcon { Clear, Night, Cloudy, Fog, Drizzle, Rain, Snow, Storm, Unknown }

/// <summary>Cuaca saat ini.</summary>
public sealed record WeatherNow(int Temperature, int Code, bool IsDay)
{
    public string Condition => Weather.Describe(Code);

    public WeatherIcon Icon => Weather.IconFor(Code, IsDay);
}

/// <summary>Lokasi untuk prakiraan cuaca.</summary>
public sealed record GeoPoint(double Latitude, double Longitude, string? City);

public static class Weather
{
    /// <summary>Kode cuaca WMO (open-meteo.com/en/docs) dalam bahasa Indonesia.</summary>
    public static string Describe(int code) => code switch
    {
        0 => "Cerah",
        1 => "Cerah berawan",
        2 => "Berawan sebagian",
        3 => "Mendung",
        45 or 48 => "Berkabut",
        51 or 53 or 55 => "Gerimis",
        56 or 57 => "Gerimis beku",
        61 or 63 => "Hujan",
        65 => "Hujan lebat",
        66 or 67 => "Hujan beku",
        71 or 73 or 75 or 77 => "Salju",
        80 or 81 => "Hujan lokal",
        82 => "Hujan deras",
        85 or 86 => "Hujan salju",
        95 => "Badai petir",
        96 or 99 => "Badai petir + es",
        _ => "Tidak diketahui",
    };

    public static WeatherIcon IconFor(int code, bool isDay) => code switch
    {
        0 or 1 => isDay ? WeatherIcon.Clear : WeatherIcon.Night,
        2 or 3 => WeatherIcon.Cloudy,
        45 or 48 => WeatherIcon.Fog,
        51 or 53 or 55 or 56 or 57 => WeatherIcon.Drizzle,
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => WeatherIcon.Rain,
        71 or 73 or 75 or 77 or 85 or 86 => WeatherIcon.Snow,
        95 or 96 or 99 => WeatherIcon.Storm,
        _ => WeatherIcon.Unknown,
    };

    public static string ForecastUrl(GeoPoint at) => string.Create(CultureInfo.InvariantCulture,
        $"https://api.open-meteo.com/v1/forecast?latitude={at.Latitude:0.####}&longitude={at.Longitude:0.####}&current=temperature_2m,weather_code,is_day&timezone=auto");

    public static string GeocodeUrl(string city) =>
        $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city.Trim())}&count=1&language=id&format=json";

    public const string IpLocationUrl = "https://ipapi.co/json/";

    /// <summary>Jawaban /v1/forecast, atau null jika bentuknya tidak sesuai.</summary>
    public static WeatherNow? ParseForecast(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("current", out var current)) return null;
            if (!current.TryGetProperty("temperature_2m", out var temp) || temp.ValueKind != JsonValueKind.Number) return null;
            int code = current.TryGetProperty("weather_code", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : -1;
            bool isDay = !current.TryGetProperty("is_day", out var d) || d.ValueKind != JsonValueKind.Number || d.GetInt32() == 1;
            return new WeatherNow((int)Math.Round(temp.GetDouble(), MidpointRounding.AwayFromZero), code, isDay);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Hasil pertama pencarian kota Open-Meteo.</summary>
    public static GeoPoint? ParseGeocode(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return null;
            var first = results[0];
            return new GeoPoint(first.GetProperty("latitude").GetDouble(), first.GetProperty("longitude").GetDouble(),
                first.TryGetProperty("name", out var name) ? name.GetString() : null);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Jawaban ipapi.co (perkiraan lokasi dari alamat IP).</summary>
    public static GeoPoint? ParseIpLocation(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("latitude", out var lat) || lat.ValueKind != JsonValueKind.Number) return null;
            if (!root.TryGetProperty("longitude", out var lon) || lon.ValueKind != JsonValueKind.Number) return null;
            return new GeoPoint(lat.GetDouble(), lon.GetDouble(), root.TryGetProperty("city", out var city) ? city.GetString() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
