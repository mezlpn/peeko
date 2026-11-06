using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Peeko.Core.Media;

namespace Peeko.App.Music;

public partial class RadioModule : UserControl
{
    private RadioService? _radio;
    private IReadOnlyList<RadioStation> _popular = [];
    private IReadOnlyList<RadioStation>? _results;
    private bool _favorites;
    private bool _loaded;
    private int _version;

    public RadioModule()
    {
        InitializeComponent();
    }

    /// <summary>Mengetik di kotak cari: pemilik mengaktifkan jendela dan menahan panel terbuka.</summary>
    public event Action<bool>? TextInputChanged;

    public void Attach(RadioService radio)
    {
        _radio = radio;
        radio.StateChanged += Rebuild;
    }

    public void SetVisible(bool visible)
    {
        if (!visible)
        {
            if (SearchBox.IsKeyboardFocused) Keyboard.ClearFocus();
            return;
        }
        if (!_loaded) _ = LoadAsync(null);
        else Rebuild();
    }

    private async Task LoadAsync(string? query)
    {
        if (_radio is null) return;
        int version = ++_version;
        _loaded = true;
        ShowMessage(query is null ? "Memuat stasiun…" : "Mencari…");
        var list = await _radio.SearchAsync(query);
        if (version != _version) return;
        if (query is null) _popular = list;
        else _results = list;
        if (list.Count == 0) _loaded = query is not null; // gagal memuat populer: coba lagi saat dibuka
        Rebuild();
    }

    private void Rebuild()
    {
        if (_radio is null) return;
        PopularTab.Foreground = B(_favorites ? "TextTertiary" : "TextPrimary");
        FavoriteTab.Foreground = B(_favorites ? "TextPrimary" : "TextTertiary");
        PopularTab.Text = _results is not null && !_favorites ? "HASIL CARI" : "POPULER";

        var stations = _favorites ? _radio.Favorites : _results ?? _popular;
        List.Children.Clear();
        // Yang sedang diputar selalu di atas.
        if (_radio.Playing is { } playing) List.Children.Add(Row(playing));
        foreach (var s in stations.Where(s => s.Id != _radio.Playing?.Id)) List.Children.Add(Row(s));
        if (stations.Count == 0 && _radio.Playing is null)
            ShowMessage(_favorites ? "Belum ada favorit. Klik bintang di samping stasiun untuk menyimpannya."
                : _results is not null ? "Tidak ada stasiun dengan nama itu." : "Stasiun belum bisa dimuat. Cek koneksi internet.");
    }

    private void ShowMessage(string text)
    {
        List.Children.Clear();
        List.Children.Add(new TextBlock
        {
            Text = text,
            Style = (Style)FindResource("Peeko.Text.Caption"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(4, 6, 4, 0),
        });
    }

    private FrameworkElement Row(RadioStation s)
    {
        bool playing = _radio?.Playing?.Id == s.Id;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var play = new System.Windows.Shapes.Path
        {
            Style = (Style)FindResource("Peeko.IconPath"),
            Data = Geometry.Parse(playing ? "M7,6 H17 V18 H7 Z" : "M8,5.5 L18.5,12 L8,18.5 Z"),
            Stroke = B(playing ? "Accent" : "TextSecondary"),
            Fill = playing ? B("Accent") : Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            LayoutTransform = new ScaleTransform(0.6, 0.6),
        };
        grid.Children.Add(play);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = s.Name.Trim(),
            Style = (Style)FindResource(playing ? "Peeko.Text.BodyStrong" : "Peeko.Text.Body"),
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        string sub = playing ? (_radio!.IsBuffering ? "Menyambung…" : "Sedang diputar") : s.Detail;
        if (sub.Length > 0)
            text.Children.Add(new TextBlock
            {
                Text = sub,
                Style = (Style)FindResource("Peeko.Text.Caption"),
                FontSize = 10.5,
                Foreground = B(playing ? "Accent" : "TextTertiary"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        bool fav = _radio?.IsFavorite(s) == true;
        var star = new Button
        {
            Style = (Style)FindResource("Peeko.Button.Icon"),
            Tag = Geometry.Parse("M12,4 L14.4,9.2 L20,9.8 L15.8,13.6 L17,19.2 L12,16.3 L7,19.2 L8.2,13.6 L4,9.8 L9.6,9.2 Z"),
            Width = 26,
            Height = 26,
            Foreground = B(fav ? "Warning" : "TextTertiary"),
            ToolTip = fav ? "Hapus dari favorit" : "Simpan ke favorit",
        };
        star.Click += (_, _) =>
        {
            _radio?.ToggleFavorite(s);
            Rebuild();
        };
        Grid.SetColumn(star, 2);
        grid.Children.Add(star);

        var row = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 0, 4, 0),
            Margin = new Thickness(0, 0, 0, 2),
            Background = playing ? B("SurfaceRaised") : Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = grid,
        };
        row.MouseEnter += (_, _) => row.Background = B("SurfaceHover");
        row.MouseLeave += (_, _) => row.Background = playing ? B("SurfaceRaised") : Brushes.Transparent;
        row.MouseLeftButtonUp += (_, e) =>
        {
            for (var n = e.OriginalSource as DependencyObject; n is not null; n = VisualTreeHelper.GetParent(n))
                if (n is Button) return;
            if (playing) _radio?.Stop();
            else _radio?.Play(s);
        };
        return row;
    }

    private void OnPopular(object sender, MouseButtonEventArgs e)
    {
        if (!_favorites && _results is not null)
        {
            // Klik "HASIL CARI": kembali ke populer.
            _results = null;
            SearchBox.Text = "";
        }
        _favorites = false;
        Rebuild();
    }

    private void OnFavorites(object sender, MouseButtonEventArgs e)
    {
        _favorites = true;
        Rebuild();
    }

    private void OnSearchMouse(object sender, MouseButtonEventArgs e)
    {
        if (SearchBox.IsKeyboardFocused) return;
        // Jendela notch tidak aktif: minta diaktifkan dulu, lalu fokus ke kotak cari.
        TextInputChanged?.Invoke(true);
        Dispatcher.BeginInvoke(() => { SearchBox.Focus(); Keyboard.Focus(SearchBox); }, System.Windows.Threading.DispatcherPriority.Input);
        e.Handled = true;
    }

    private void OnSearchFocus(object sender, KeyboardFocusChangedEventArgs e) => TextInputChanged?.Invoke(true);

    private void OnSearchBlur(object sender, KeyboardFocusChangedEventArgs e) => TextInputChanged?.Invoke(false);

    private void OnSearchText(object sender, TextChangedEventArgs e) =>
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnSearchKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _favorites = false;
            var q = SearchBox.Text.Trim();
            if (q.Length == 0) _results = null;
            _ = q.Length == 0 ? Task.Run(() => Dispatcher.Invoke(Rebuild)) : LoadAsync(q);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SearchBox.Text = "";
            _results = null;
            Keyboard.ClearFocus();
            Rebuild();
            e.Handled = true;
        }
    }

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
