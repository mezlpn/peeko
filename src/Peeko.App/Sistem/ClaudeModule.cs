using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Peeko.Platform.Network;

namespace Peeko.App.Sistem;

/// <summary>
/// Permintaan izin Claude Code di notch: alat + ringkasan (perintah, file, alamat), lalu Tolak / Izinkan.
/// Hitung mundur menunjukkan kapan Peeko menyerah dan dialog biasa di terminal yang dipakai.
/// </summary>
public sealed class ClaudeModule : Grid
{
    public const double ContentWidth = 440;
    public const double ContentHeight = 92;

    private readonly TextBlock _head, _title, _summary, _countdown;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _deadline;

    public ClaudeModule()
    {
        Width = ContentWidth;
        Height = ContentHeight;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        _head = new TextBlock { Style = S("Peeko.Text.Overline"), Foreground = B("Accent") };
        _title = new TextBlock { Style = S("Peeko.Text.BodyStrong"), Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        _summary = new TextBlock
        {
            Style = S("Peeko.Text.Caption"),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            Foreground = B("TextSecondary"),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 34,
            Margin = new Thickness(0, 4, 0, 0),
        };
        text.Children.Add(_head);
        text.Children.Add(_title);
        text.Children.Add(_summary);
        Children.Add(text);

        var actions = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var deny = new Button { Content = "Tolak", Style = S("Peeko.Button.PillSecondary"), Height = 30, Padding = new Thickness(14, 0, 14, 0) };
        var allow = new Button { Content = "Izinkan", Style = S("Peeko.Button.Pill"), Height = 30, Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(8, 0, 0, 0) };
        deny.Click += (_, _) => Answered?.Invoke(ClaudeDecision.Deny);
        allow.Click += (_, _) => Answered?.Invoke(ClaudeDecision.Allow);
        buttons.Children.Add(deny);
        buttons.Children.Add(allow);
        actions.Children.Add(buttons);
        _countdown = new TextBlock { Style = S("Peeko.Text.Caption"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 4, 0) };
        actions.Children.Add(_countdown);
        SetColumn(actions, 1);
        Children.Add(actions);

        _tick.Tick += (_, _) => UpdateCountdown();
    }

    public event Action<ClaudeDecision>? Answered;

    public void Show(ClaudePermission request)
    {
        _head.Text = string.IsNullOrEmpty(request.Folder) ? "CLAUDE CODE" : $"CLAUDE CODE · {request.Folder.ToUpperInvariant()}";
        _title.Text = request.Tool switch
        {
            "Bash" or "PowerShell" => "Ingin menjalankan perintah",
            "Write" => "Ingin membuat file",
            "Edit" or "MultiEdit" or "NotebookEdit" => "Ingin mengubah file",
            "WebFetch" => "Ingin membuka halaman web",
            "WebSearch" => "Ingin mencari di web",
            _ => $"Ingin memakai {request.Tool}",
        };
        _summary.Text = request.Summary;
        _deadline = DateTime.Now + ClaudeBridge.AnswerTimeout;
        UpdateCountdown();
        _tick.Start();
    }

    public void Hide() => _tick.Stop();

    private void UpdateCountdown()
    {
        int left = Math.Max(0, (int)Math.Ceiling((_deadline - DateTime.Now).TotalSeconds));
        _countdown.Text = $"{left} dtk, lalu tanya di terminal";
    }

    private static Style S(string key) => (Style)Application.Current.Resources[key];

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
