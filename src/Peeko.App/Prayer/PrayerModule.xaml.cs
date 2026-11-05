using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Peeko.Core.Prayer;

namespace Peeko.App.Prayer;

public partial class PrayerModule : UserControl
{
    private static readonly string[] HijriMonths =
    [
        "Muharram", "Safar", "Rabiulawal", "Rabiulakhir", "Jumadilawal", "Jumadilakhir",
        "Rajab", "Syakban", "Ramadan", "Syawal", "Zulkaidah", "Zulhijah",
    ];

    private PrayerController? _prayer;

    public PrayerModule()
    {
        InitializeComponent();
    }

    public event Action? OpenSettingsRequested;

    public void Attach(PrayerController prayer)
    {
        _prayer = prayer;
        Refresh();
    }

    public void Refresh()
    {
        if (_prayer is null) return;
        bool has = _prayer.HasLocation;
        ScheduleView.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        NoLocationView.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        if (!has) return;

        var now = DateTime.Now;
        var next = _prayer.Next();
        NextName.Text = next?.Label ?? "–";
        NextLeft.Text = next is null ? "" : PrayerText.Left(next.At - now);
        PlaceText.Text = _prayer.Place is { Length: > 0 } place ? place : "Lokasi dari perkiraan IP";
        HijriText.Text = Hijri(now);
        StopAdhan.Visibility = _prayer.AdhanPlaying ? Visibility.Visible : Visibility.Collapsed;

        Rows.Children.Clear();
        foreach (var p in _prayer.Today())
        {
            bool isNext = next is not null && p.At == next.At;
            bool passed = p.At <= now && !isNext;
            var row = new Grid { Height = 20 };
            if (isNext)
            {
                row.Children.Add(new Border { CornerRadius = new CornerRadius(6), Background = B("RestMuted"), Margin = new Thickness(-8, 0, -8, 0) });
            }
            var brush = isNext ? B("Rest") : passed || !p.IsPrayer ? B("TextTertiary") : B("TextPrimary");
            row.Children.Add(new TextBlock
            {
                Text = p.Label,
                Style = (Style)FindResource("Peeko.Text.Body"),
                FontSize = 12,
                Foreground = brush,
                FontWeight = isNext ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            });
            row.Children.Add(new TextBlock
            {
                Text = p.At.ToString("HH.mm"),
                Style = (Style)FindResource("Peeko.Text.Body"),
                FontSize = 12,
                Foreground = brush,
                FontWeight = isNext ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            });
            Rows.Children.Add(row);
        }
    }

    /// <summary>"14 Jumadilawal 1448 H". Hari Hijriah berganti saat Maghrib, tapi di sini mengikuti tanggal Masehi.</summary>
    private static string Hijri(DateTime now)
    {
        var cal = new UmAlQuraCalendar();
        try
        {
            return $"{cal.GetDayOfMonth(now)} {HijriMonths[cal.GetMonth(now) - 1]} {cal.GetYear(now)} H";
        }
        catch (ArgumentOutOfRangeException)
        {
            return "";
        }
    }

    private void OnStopAdhan(object sender, RoutedEventArgs e) => _prayer?.StopAdhan();

    private void OnOpenSettings(object sender, RoutedEventArgs e) => OpenSettingsRequested?.Invoke();

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
