using Peeko.Core.Status;

namespace Peeko.Core.Tests;

public class MonthGridTests
{
    [Fact]
    public void October_2026_starts_on_thursday_and_needs_five_weeks()
    {
        var weeks = MonthGrid.Build(new DateOnly(2026, 10, 22));
        Assert.Equal(5, weeks.Count);
        Assert.Equal(0, weeks[0][3].Day);   // Rabu kosong
        Assert.Equal(1, weeks[0][4].Day);   // 1 Oktober = Kamis
        Assert.Equal(31, weeks[4][6].Day);  // 31 Oktober = Sabtu
    }

    [Fact]
    public void Marks_today_and_sundays()
    {
        var weeks = MonthGrid.Build(new DateOnly(2026, 10, 22));
        var today = weeks.SelectMany(w => w).Single(c => c.IsToday);
        Assert.Equal(22, today.Day);
        Assert.All(weeks, w => Assert.True(w[0].IsSunday));
        Assert.All(weeks, w => Assert.False(w[1].IsSunday));
    }

    [Fact]
    public void A_month_can_need_six_weeks()
    {
        // Agustus 2026: tanggal 1 jatuh hari Sabtu, 31 hari.
        Assert.Equal(6, MonthGrid.Build(2026, 8, new DateOnly(2026, 8, 1)).Count);
    }

    [Fact]
    public void No_today_marker_in_another_month()
    {
        var weeks = MonthGrid.Build(2026, 11, new DateOnly(2026, 10, 22));
        Assert.DoesNotContain(weeks.SelectMany(w => w), c => c.IsToday);
    }
}

public class WeatherTests
{
    [Fact]
    public void Parses_open_meteo_current_weather()
    {
        const string json = """
            {"latitude":-7.75,"longitude":110.375,"current_units":{"temperature_2m":"°C"},
             "current":{"time":"2026-10-22T14:00","interval":900,"temperature_2m":31.5,"weather_code":80,"is_day":1}}
            """;
        var now = Weather.ParseForecast(json)!;
        Assert.Equal(32, now.Temperature);
        Assert.Equal("Hujan lokal", now.Condition);
        Assert.Equal(WeatherIcon.Rain, now.Icon);
    }

    [Fact]
    public void Clear_night_uses_the_moon()
    {
        Assert.Equal(WeatherIcon.Night, Weather.IconFor(0, isDay: false));
        Assert.Equal(WeatherIcon.Clear, Weather.IconFor(1, isDay: true));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"current":{"weather_code":3}}""")]
    [InlineData("not json")]
    public void Rejects_unexpected_forecast_shapes(string json) => Assert.Null(Weather.ParseForecast(json));

    [Fact]
    public void Parses_first_geocoding_result()
    {
        const string json = """{"results":[{"id":1,"name":"Yogyakarta","latitude":-7.80139,"longitude":110.36472,"country":"Indonesia"}]}""";
        var at = Weather.ParseGeocode(json)!;
        Assert.Equal("Yogyakarta", at.City);
        Assert.Equal(-7.80139, at.Latitude, 5);
        Assert.Null(Weather.ParseGeocode("""{"generationtime_ms":0.5}"""));
    }

    [Fact]
    public void Parses_ip_location()
    {
        var at = Weather.ParseIpLocation("""{"ip":"x","city":"Sleman","latitude":-7.71,"longitude":110.35}""")!;
        Assert.Equal("Sleman", at.City);
        Assert.Null(Weather.ParseIpLocation("""{"error":true,"reason":"RateLimited"}"""));
    }

    [Fact]
    public void Forecast_url_uses_invariant_decimals()
    {
        var url = Weather.ForecastUrl(new GeoPoint(-7.80139, 110.36472, null));
        Assert.Contains("latitude=-7.8014&longitude=110.3647", url);
    }

    [Fact]
    public void Geocode_url_escapes_the_city() =>
        Assert.Contains("name=Kota%20Gede", Weather.GeocodeUrl(" Kota Gede "));
}
