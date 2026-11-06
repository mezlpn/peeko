using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Peeko.Core.Productivity;

namespace Peeko.App.Sistem;

/// <summary>
/// Tab AI, 440 × 170: TANYA / TERJEMAH, percakapan singkat, kotak tanya. Aksi dari AI (tambah tugas,
/// catatan, musik, volume, buka aplikasi) dijalankan langsung dan masing-masing punya tombol Urungkan.
/// </summary>
public sealed class AiModule : Grid
{
    public const double ContentWidth = 440;
    public const double ContentHeight = 170;

    private readonly TextBlock _askTab, _translateTab;
    private readonly StackPanel _list = new();
    private readonly ScrollViewer _scroll;
    private readonly TextBox _input;
    private readonly TextBlock _placeholder;
    private readonly List<AiMessage> _history = [];
    private AiService? _ai;
    private bool _translate;
    private bool _busy;
    private CancellationTokenSource? _cts;

    public AiModule()
    {
        Width = ContentWidth;
        Height = ContentHeight;
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });

        var tabs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _askTab = Tab("TANYA", () => SetMode(false));
        _translateTab = Tab("TERJEMAH", () => SetMode(true));
        _translateTab.Margin = new Thickness(14, 0, 0, 0);
        tabs.Children.Add(_askTab);
        tabs.Children.Add(_translateTab);
        Children.Add(tabs);
        var clear = new Button { Content = "Bersihkan", Style = S("Peeko.Button.Text"), Height = 22, Padding = new Thickness(8, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Right };
        clear.Click += (_, _) =>
        {
            _history.Clear();
            Rebuild();
        };
        Children.Add(clear);

        _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = _list, Margin = new Thickness(0, 6, 0, 0) };
        SetRow(_scroll, 1);
        Children.Add(_scroll);

        var inputGrid = new Grid();
        inputGrid.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = B("SurfaceRaised"),
            BorderBrush = B("StrokeStrong"),
            BorderThickness = new Thickness(1),
        });
        _input = new TextBox
        {
            Margin = new Thickness(14, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = B("TextPrimary"),
            CaretBrush = B("Accent"),
            FontFamily = (FontFamily)Application.Current.Resources["Peeko.Font"],
            FontSize = 13,
            MaxLength = 2000,
        };
        _input.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (_input.IsKeyboardFocused) return;
            TextInputChanged?.Invoke(true);
            Dispatcher.BeginInvoke(() => { _input.Focus(); Keyboard.Focus(_input); }, DispatcherPriority.Input);
            e.Handled = true;
        };
        _input.GotKeyboardFocus += (_, _) => TextInputChanged?.Invoke(true);
        _input.LostKeyboardFocus += (_, _) => TextInputChanged?.Invoke(false);
        _input.TextChanged += (_, _) => _placeholder!.Visibility = _input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _input.KeyDown += OnKey;
        inputGrid.Children.Add(_input);
        _placeholder = new TextBlock
        {
            Style = S("Peeko.Text.Body"),
            Foreground = B("TextTertiary"),
            Margin = new Thickness(16, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        inputGrid.Children.Add(_placeholder);
        SetRow(inputGrid, 3);
        Children.Add(inputGrid);
        SetMode(false);
    }

    /// <summary>Mengetik: pemilik mengaktifkan jendela dan menahan panel terbuka.</summary>
    public event Action<bool>? TextInputChanged;

    public event Action? OpenSettingsRequested;

    /// <summary>Jalankan aksi dari AI: berhasil? dan cara mengurungkannya (null = tidak bisa diurungkan).</summary>
    public Func<AiAction, (bool Ok, Action? Undo)>? Execute { get; set; }

    public void Attach(AiService ai)
    {
        _ai = ai;
        Rebuild();
    }

    public void SetVisible(bool visible)
    {
        if (visible) Rebuild();
        else if (_input.IsKeyboardFocused) Keyboard.ClearFocus();
    }

    private void SetMode(bool translate)
    {
        _translate = translate;
        _askTab.Foreground = B(translate ? "TextTertiary" : "TextPrimary");
        _translateTab.Foreground = B(translate ? "TextPrimary" : "TextTertiary");
        _placeholder.Text = translate ? "Tempel teks untuk diterjemahkan…" : "Tanya, atau minta \"ingatkan besok jam 7 kumpul laporan\"";
    }

    private async void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Keyboard.ClearFocus();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Enter || _busy || _ai is null) return;
        e.Handled = true;
        var text = _input.Text.Trim();
        if (text.Length == 0) return;
        if (!_ai.IsConfigured)
        {
            Rebuild();
            return;
        }
        _input.Text = "";
        _history.Add(new AiMessage(true, text));
        _busy = true;
        Rebuild(thinking: true);
        _cts = new CancellationTokenSource();
        string reply;
        IReadOnlyList<AiAction> actions = [];
        try
        {
            var raw = await _ai.AskAsync(_history, _translate, _cts.Token);
            (reply, actions) = _translate ? (raw.Trim(), []) : AiActions.Parse(raw);
            if (reply.Length == 0 && actions.Count > 0) reply = "Beres.";
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
                                       or NullReferenceException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            reply = ex is TaskCanceledException ? "Penyedia AI terlalu lama menjawab." : $"Gagal: {ex.Message}";
        }
        finally
        {
            _busy = false;
        }
        _history.Add(new AiMessage(false, reply));
        Rebuild(actions: actions);
    }

    private void Rebuild(bool thinking = false, IReadOnlyList<AiAction>? actions = null)
    {
        _list.Children.Clear();
        if (_ai is null || !_ai.IsConfigured)
        {
            _list.Children.Add(new TextBlock
            {
                Text = "Hubungkan Claude, Gemini, ChatGPT, atau Ollama lokal di Pengaturan → AI. Kunci API hanya dikirim ke penyedia yang kamu pilih.",
                Style = S("Peeko.Text.Caption"),
                TextWrapping = TextWrapping.Wrap,
            });
            var open = new Button { Content = "Buka Pengaturan", Style = S("Peeko.Button.Pill"), Height = 26, Padding = new Thickness(12, 0, 12, 0), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
            open.Click += (_, _) => OpenSettingsRequested?.Invoke();
            _list.Children.Add(open);
            return;
        }
        if (_history.Count == 0 && !thinking)
        {
            _list.Children.Add(new TextBlock
            {
                Text = _translate ? "Teks Indonesia diterjemahkan ke Inggris, bahasa lain ke Indonesia."
                    : "Contoh: \"jelaskan rumus Pythagoras\", \"catat ide: video notch\", \"kecilkan volume ke 30\", \"buka Spotify\".",
                Style = S("Peeko.Text.Caption"),
                TextWrapping = TextWrapping.Wrap,
            });
        }
        foreach (var m in _history.TakeLast(6)) _list.Children.Add(Bubble(m));
        if (thinking) _list.Children.Add(new TextBlock { Text = "Berpikir…", Style = S("Peeko.Text.Caption"), Margin = new Thickness(2, 4, 0, 0) });
        if (actions is { Count: > 0 })
        {
            var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            foreach (var a in actions) row.Children.Add(ActionChip(a));
            _list.Children.Add(row);
        }
        Dispatcher.BeginInvoke(_scroll.ScrollToEnd, DispatcherPriority.Loaded);
    }

    private FrameworkElement Bubble(AiMessage m)
    {
        var text = new TextBlock
        {
            Text = m.Text,
            Style = S("Peeko.Text.Body"),
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = B(m.FromUser ? "TextPrimary" : "TextSecondary"),
        };
        return new Border
        {
            Child = text,
            Background = m.FromUser ? B("SurfaceRaised") : Brushes.Transparent,
            CornerRadius = new CornerRadius(10),
            Padding = m.FromUser ? new Thickness(10, 5, 10, 5) : new Thickness(2, 3, 2, 3),
            Margin = new Thickness(m.FromUser ? 60 : 0, 0, 0, 4),
            HorizontalAlignment = m.FromUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };
    }

    private FrameworkElement ActionChip(AiAction a)
    {
        var (ok, undo) = Execute?.Invoke(a) ?? (false, null);
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text = AiActions.Label(a),
            Style = S("Peeko.Text.Caption"),
            Foreground = B("Accent"),
            MaxWidth = 260,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var chip = new Border
        {
            Height = 24,
            Padding = new Thickness(10, 0, 6, 0),
            Margin = new Thickness(0, 0, 6, 4),
            CornerRadius = new CornerRadius(12),
            Background = B("AccentMuted"),
            Child = panel,
        };
        if (undo is not null)
        {
            var link = new TextBlock
            {
                Text = "Urungkan",
                Style = S("Peeko.Text.Caption"),
                FontWeight = FontWeights.SemiBold,
                Foreground = B("TextPrimary"),
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 2, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            link.MouseLeftButtonUp += (_, _) =>
            {
                undo();
                link.Text = "Diurungkan";
                link.Cursor = null;
                link.IsHitTestVisible = false;
            };
            panel.Children.Add(link);
        }
        else if (!ok)
        {
            panel.Children.Add(new TextBlock { Text = a.Kind == AiActionKind.Open ? " · tidak ketemu" : " · gagal", Style = S("Peeko.Text.Caption"), VerticalAlignment = VerticalAlignment.Center });
        }
        else
        {
            panel.Margin = new Thickness(0, 0, 4, 0);
        }
        return chip;
    }

    private TextBlock Tab(string text, Action click)
    {
        var t = new TextBlock { Text = text, Style = S("Peeko.Text.Overline"), Cursor = Cursors.Hand };
        t.MouseLeftButtonUp += (_, _) => click();
        return t;
    }

    private static Style S(string key) => (Style)Application.Current.Resources[key];

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
