// Diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): mode Calendar dan Status island.

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Peeko.Core.Status;
using Peeko.Platform.Control;

namespace Peeko.App.Today;

/// <summary>Kalender bulan ini, cuaca, dan baterai. Semua dibaca ulang setiap kali tab terlihat.</summary>
public partial class TodayModule : UserControl
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    private WeatherService? _weather;
    private DateOnly _shownDay;
    private bool _visible;

    public TodayModule()
    {
        InitializeComponent();
        foreach (var initial in MonthGrid.WeekdayInitials)
        {
            WeekdayRow.Children.Add(new TextBlock
            {
                Text = initial,
                Style = (Style)FindResource("Peeko.Text.Caption"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
    }

    public void Attach(WeatherService weather) => _weather = weather;

    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (!visible) return;
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today != _shownDay) ShowCalendar(today);
        ShowBattery();
        _ = ShowWeatherAsync();
    }

    private void ShowCalendar(DateOnly today)
    {
        _shownDay = today;
        MonthTitle.Text = $"{MonthGrid.MonthName(today.Month)} {today.Year}";
        var date = today.ToDateTime(TimeOnly.MinValue);
        DayName.Text = date.ToString("dddd", Indonesian).ToUpper(Indonesian);
        DateText.Text = $"{today.Day} {MonthGrid.MonthName(today.Month)}";

        DaysGrid.Children.Clear();
        var caption = (Style)FindResource("Peeko.Text.Body");
        var primary = (Brush)FindResource("Peeko.Brush.TextPrimary");
        var sunday = (Brush)FindResource("Peeko.Brush.Danger");
        foreach (var week in MonthGrid.Build(today))
        {
            foreach (var cell in week)
            {
                var text = new TextBlock
                {
                    Style = caption,
                    Text = cell.Day == 0 ? "" : cell.Day.ToString(CultureInfo.InvariantCulture),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = cell.IsSunday ? sunday : primary,
                    Opacity = cell.IsSunday && !cell.IsToday ? 0.8 : 1,
                };
                var box = new Border { Height = 22, Width = 26, CornerRadius = new CornerRadius(11), Child = text };
                if (cell.IsToday)
                {
                    box.Background = (Brush)FindResource("Peeko.Brush.Accent");
                    text.Foreground = (Brush)FindResource("Peeko.Brush.AccentInk");
                    text.FontWeight = FontWeights.SemiBold;
                }
                DaysGrid.Children.Add(box);
            }
        }
    }

    private void ShowBattery()
    {
        var battery = SystemControls.GetBattery();
        if (battery is not { } b)
        {
            BatteryPercent.Text = "–";
            BatteryStatus.Text = "Tidak ada baterai";
            BatteryFill.Width = 0;
            return;
        }
        BatteryPercent.Text = $"{b.Percent}%";
        BatteryFill.Width = Math.Max(1, 19 * b.Percent / 100.0);
        BatteryFill.Background = (Brush)FindResource(b.Percent <= 20 && !b.PluggedIn ? "Peeko.Brush.Danger" : "Peeko.Brush.Accent");
        BatteryStatus.Text = (b.PluggedIn, b.Charging) switch
        {
            (_, true) => "Sedang dicas",
            (true, false) => b.Percent >= 100 ? "Penuh, tercolok" : "Tercolok, tidak mengisi",
            _ => b.Remaining is { } left ? $"Sisa ±{FormatDuration(left)}" : "Memakai baterai",
        };
    }

    private static string FormatDuration(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours} j {t.Minutes} mnt" : $"{t.Minutes} mnt";

    private async Task ShowWeatherAsync()
    {
        if (_weather is null) return;
        if (_weather.Latest is null) WeatherPlace.Text = "Memuat…";
        var now = await _weather.GetAsync();
        if (!_visible) return;
        if (now is null)
        {
            WeatherCondition.Text = "Cuaca";
            WeatherPlace.Text = _weather.IsEnabled ? "Belum bisa dimuat" : "Dimatikan di Pengaturan";
            WeatherTemp.Text = "–";
            WeatherIconPath.Data = (Geometry)FindResource("Thermo");
            return;
        }
        WeatherCondition.Text = now.Condition;
        WeatherPlace.Text = _weather.Place ?? "Lokasimu";
        WeatherTemp.Text = $"{now.Temperature}°";
        WeatherIconPath.Data = (Geometry)FindResource(now.Icon switch
        {
            WeatherIcon.Clear => "Peeko.Icon.Sun",
            WeatherIcon.Night => "Peeko.Icon.Moon",
            WeatherIcon.Cloudy => "Cloud",
            WeatherIcon.Fog => "Fog",
            WeatherIcon.Drizzle => "CloudDrizzle",
            WeatherIcon.Rain => "CloudRain",
            WeatherIcon.Snow => "CloudSnow",
            WeatherIcon.Storm => "CloudStorm",
            _ => "Thermo",
        });
    }
}
