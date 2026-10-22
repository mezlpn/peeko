// Diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): mode Calendar island.

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Peeko.Core.Status;

namespace Peeko.App.Today;

/// <summary>Kalender bulan ini; dibangun ulang hanya saat hari berganti.</summary>
public partial class TodayModule : UserControl
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");
    private const double RowHeight = 22;

    private DateOnly _shownDay;

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
        Refresh();
    }

    /// <summary>Tinggi kalender (judul + hari + 4–6 minggu), dipakai NotchWindow untuk ukuran notch.</summary>
    public double ContentHeight => 22 + 16 + DaysGrid.Children.Count / 7 * RowHeight;

    public void Refresh()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today == _shownDay) return;
        _shownDay = today;
        MonthTitle.Text = $"{MonthGrid.MonthName(today.Month)} {today.Year}";
        DayName.Text = today.ToDateTime(TimeOnly.MinValue).ToString("dddd", Indonesian);

        DaysGrid.Children.Clear();
        var body = (Style)FindResource("Peeko.Text.Body");
        var primary = (Brush)FindResource("Peeko.Brush.TextPrimary");
        var sunday = (Brush)FindResource("Peeko.Brush.Danger");
        foreach (var week in MonthGrid.Build(today))
        {
            foreach (var cell in week)
            {
                var text = new TextBlock
                {
                    Style = body,
                    FontSize = 12,
                    Text = cell.Day == 0 ? "" : cell.Day.ToString(CultureInfo.InvariantCulture),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = cell.IsSunday ? sunday : primary,
                    Opacity = cell.IsSunday && !cell.IsToday ? 0.8 : 1,
                };
                var box = new Border { Height = RowHeight - 2, Width = 24, Margin = new Thickness(0, 1, 0, 1), CornerRadius = new CornerRadius(10), Child = text };
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
}
