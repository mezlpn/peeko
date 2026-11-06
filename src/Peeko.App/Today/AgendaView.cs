using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Peeko.Core.Status;

namespace Peeko.App.Today;

/// <summary>
/// Agenda hari ini di bawah kalender (hanya kalau ada tautan kalender): jam · judul · tombol Gabung
/// untuk rapat online yang sebentar lagi atau sedang berlangsung.
/// </summary>
public sealed class AgendaView : StackPanel
{
    public const int MaxRows = 3;
    private const double RowHeight = 28;
    private CalendarService? _calendar;

    public void Attach(CalendarService calendar)
    {
        _calendar = calendar;
        calendar.Updated += () => Refresh();
    }

    /// <summary>Tinggi termasuk jarak di atasnya; 0 jika kalender tidak dipakai.</summary>
    public double ContentHeight => _calendar?.IsEnabled == true ? 12 + 20 + Math.Max(1, Rows) * RowHeight : 0;

    private int Rows => Math.Min(MaxRows, _calendar?.Today(DateTime.Now).Count ?? 0);

    public event Action? SizeChanged2;

    public void Refresh()
    {
        Children.Clear();
        if (_calendar is null || !_calendar.IsEnabled)
        {
            Visibility = Visibility.Collapsed;
            SizeChanged2?.Invoke();
            return;
        }
        Visibility = Visibility.Visible;
        Margin = new Thickness(0, 12, 0, 0);
        var now = DateTime.Now;
        var today = _calendar.Today(now);

        var head = new Grid { Height = 20 };
        head.Children.Add(new TextBlock { Text = "ACARA HARI INI", Style = S("Peeko.Text.Overline"), VerticalAlignment = VerticalAlignment.Center });
        string right = _calendar.LastError ?? (today.Count > MaxRows ? $"+{today.Count - MaxRows} lagi" : "");
        head.Children.Add(new TextBlock
        {
            Text = right,
            Style = S("Peeko.Text.Caption"),
            Foreground = B(_calendar.LastError is null ? "TextTertiary" : "Warning"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Children.Add(head);

        if (today.Count == 0)
        {
            Children.Add(new TextBlock
            {
                Text = _calendar.Next(now) is { } next ? $"Tidak ada acara lagi. Berikutnya: {next.Title}, {next.Start:ddd HH.mm}" : "Tidak ada acara lagi hari ini",
                Style = S("Peeko.Text.Caption"),
                Height = RowHeight,
                Padding = new Thickness(0, 6, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        foreach (var e in today.Take(MaxRows)) Children.Add(Row(e, now));
        SizeChanged2?.Invoke();
    }

    private FrameworkElement Row(CalendarEvent e, DateTime now)
    {
        bool live = e.IsNow(now);
        var grid = new Grid { Height = RowHeight };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock
        {
            Text = e.AllDay ? "Sepanjang hari" : $"{e.Start:HH.mm}–{e.End:HH.mm}",
            Style = S("Peeko.Text.Caption"),
            Foreground = B(live ? "Accent" : "TextSecondary"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var title = new TextBlock
        {
            Text = e.Title,
            Style = S(live ? "Peeko.Text.BodyStrong" : "Peeko.Text.Body"),
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = e.Location is null ? e.Title : $"{e.Title}\n{e.Location}",
        };
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);
        // Gabung: muncul 15 menit sebelum sampai acara selesai.
        if (e.JoinUrl is { } url && now >= e.Start - TimeSpan.FromMinutes(15) && now < e.End)
        {
            var join = new Button
            {
                Content = "Gabung",
                Style = S("Peeko.Button.Pill"),
                Height = 22,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(8, 0, 0, 0),
                ToolTip = url,
            };
            join.Click += (_, _) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
            };
            Grid.SetColumn(join, 2);
            grid.Children.Add(join);
        }
        return grid;
    }

    private static Style S(string key) => (Style)Application.Current.Resources[key];

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
