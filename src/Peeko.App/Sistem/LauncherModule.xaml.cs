using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Peeko.App.Productivity;
using Peeko.Core.Productivity;
using Peeko.Core.Storage;

namespace Peeko.App.Sistem;

/// <summary>
/// Peluncur aplikasi. Klik ubin untuk membuka, klik kanan untuk menggeser atau menghapus, ubin "+"
/// untuk memilih aplikasi dari menu Start (atau file/folder apa saja).
/// </summary>
public partial class LauncherModule : UserControl
{
    public const int PerRow = 6;
    private const double RowHeight = 76;

    private readonly JsonFileStore<LauncherData> _store;
    private readonly AutoSaver _saver;
    private readonly Dictionary<string, ImageSource?> _icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonFileStore<BookmarkData> _linkStore;
    private readonly AutoSaver _linkSaver;
    private bool _showLinks;
    private string? _category;

    public LauncherModule()
    {
        InitializeComponent();
        _store = new JsonFileStore<LauncherData>(App.DataDirectory, "launcher.json");
        Launcher = new Launcher(_store.Load());
        _saver = new AutoSaver(() => _store.Save(Launcher.Data));
        Launcher.Changed += () =>
        {
            _saver.MarkDirty();
            Rebuild();
            SizeChanged?.Invoke();
        };
        _linkStore = new JsonFileStore<BookmarkData>(App.DataDirectory, "bookmarks.json");
        Links = new Bookmarks(_linkStore.Load());
        _linkSaver = new AutoSaver(() => _linkStore.Save(Links.Data));
        Links.Changed += () =>
        {
            _linkSaver.MarkDirty();
            RebuildLinks();
        };
        Rebuild();
        ShowView();
    }

    public Launcher Launcher { get; }

    public Bookmarks Links { get; }

    /// <summary>Mengetik tautan baru: pemilik mengaktifkan jendela dan menahan panel terbuka.</summary>
    public event Action<bool>? TextInputChanged;

    /// <summary>Jumlah baris berubah: notch menyesuaikan tinggi.</summary>
    public new event Action? SizeChanged;

    /// <summary>Menu klik kanan atau dialog pilih aplikasi terbuka: panel ditahan terbuka.</summary>
    public event Action<bool>? MenuOpenChanged;

    public double ContentHeight => 30 + (_showLinks
        ? 150
        : Math.Ceiling((Launcher.Apps.Count + (Launcher.IsFull ? 0 : 1)) / (double)PerRow) * RowHeight);

    public void Flush()
    {
        _saver.Flush();
        _linkSaver.Flush();
    }

    // ── Aplikasi / Tautan ──────────────────────────────────────────────────

    private void OnAppsTab(object sender, MouseButtonEventArgs e)
    {
        _showLinks = false;
        EndLinkInput();
        ShowView();
    }

    private void OnLinksTab(object sender, MouseButtonEventArgs e)
    {
        _showLinks = true;
        ShowView();
    }

    private void ShowView()
    {
        AppsTab.Foreground = B(_showLinks ? "TextTertiary" : "TextPrimary");
        LinksTab.Foreground = B(_showLinks ? "TextPrimary" : "TextTertiary");
        Tiles.Visibility = _showLinks ? Visibility.Collapsed : Visibility.Visible;
        LinksView.Visibility = _showLinks ? Visibility.Visible : Visibility.Collapsed;
        AddLinkButton.Visibility = _showLinks ? Visibility.Visible : Visibility.Collapsed;
        if (_showLinks) RebuildLinks();
        SizeChanged?.Invoke();
    }

    private void RebuildLinks()
    {
        if (!_showLinks) return;
        var categories = Links.Categories;
        if (_category is not null && !categories.Contains(_category)) _category = null;
        Chips.Children.Clear();
        Chips.Visibility = categories.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (categories.Count > 0)
        {
            Chips.Children.Add(Chip("Semua", null));
            foreach (var c in categories) Chips.Children.Add(Chip(c, c));
        }

        LinkList.Children.Clear();
        var items = Links.In(_category);
        if (items.Count == 0 && LinkInputRow.Visibility != Visibility.Visible)
        {
            LinkList.Children.Add(new TextBlock
            {
                Text = "Simpan tautan yang sering dibuka. Klik + lalu ketik alamatnya, judul, dan #kategori, mis. \"kampus.ac.id Portal kuliah #Kuliah\".",
                Style = (Style)FindResource("Peeko.Text.Caption"),
                TextWrapping = TextWrapping.Wrap,
            });
        }
        foreach (var b in items) LinkList.Children.Add(LinkRow(b));
    }

    private FrameworkElement Chip(string label, string? category)
    {
        bool on = _category == category;
        var chip = new Border
        {
            Height = 22,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0, 0, 6, 0),
            CornerRadius = new CornerRadius(11),
            Background = B(on ? "AccentMuted" : "SurfaceRaised"),
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = label,
                Style = (Style)FindResource("Peeko.Text.Caption"),
                Foreground = B(on ? "Accent" : "TextSecondary"),
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        chip.MouseLeftButtonUp += (_, _) =>
        {
            _category = category;
            RebuildLinks();
        };
        return chip;
    }

    private FrameworkElement LinkRow(Bookmark b)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new System.Windows.Shapes.Path
        {
            Style = (Style)FindResource("Peeko.IconPath"),
            Data = (Geometry)FindResource("Peeko.Icon.Link"),
            Stroke = B("TextSecondary"),
            VerticalAlignment = VerticalAlignment.Center,
            LayoutTransform = new ScaleTransform(0.65, 0.65),
        });
        var title = new TextBlock
        {
            Text = b.Title,
            Style = (Style)FindResource("Peeko.Text.Body"),
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);
        if (b.Category.Length > 0 && _category is null)
        {
            var cat = new TextBlock
            {
                Text = b.Category,
                Style = (Style)FindResource("Peeko.Text.Caption"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0),
            };
            Grid.SetColumn(cat, 2);
            grid.Children.Add(cat);
        }
        var remove = new Button
        {
            Style = (Style)FindResource("Peeko.Button.Icon"),
            Tag = FindResource("Peeko.Icon.Close"),
            Width = 22,
            Height = 22,
            Foreground = B("TextTertiary"),
            ToolTip = "Hapus tautan",
            Opacity = 0,
        };
        remove.Click += (_, _) => Links.Remove(b.Id);
        Grid.SetColumn(remove, 3);
        grid.Children.Add(remove);

        var row = new Border
        {
            Height = 28,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 0, 4, 0),
            Margin = new Thickness(0, 0, 0, 2),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = b.Url,
            Child = grid,
        };
        row.MouseEnter += (_, _) => { row.Background = B("SurfaceRaised"); remove.Opacity = 1; };
        row.MouseLeave += (_, _) => { row.Background = Brushes.Transparent; remove.Opacity = 0; };
        row.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && IsInside<Button>(d)) return;
            try
            {
                Process.Start(new ProcessStartInfo(b.Url) { UseShellExecute = true })?.Dispose();
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        };
        return row;
    }

    private static bool IsInside<T>(DependencyObject node) where T : DependencyObject
    {
        for (var n = node; n is not null; n = VisualTreeHelper.GetParent(n))
            if (n is T) return true;
        return false;
    }

    private void OnAddLink(object sender, RoutedEventArgs e)
    {
        if (LinkInputRow.Visibility == Visibility.Visible) return;
        LinkInputRow.Visibility = Visibility.Visible;
        LinkInput.Text = _category is { } c ? $" #{c.Replace(' ', '_')}" : "";
        LinkInput.CaretIndex = 0;
        TextInputChanged?.Invoke(true);
        Dispatcher.BeginInvoke(() => { LinkInput.Focus(); Keyboard.Focus(LinkInput); }, System.Windows.Threading.DispatcherPriority.Input);
        RebuildLinks();
    }

    private void EndLinkInput()
    {
        if (LinkInputRow.Visibility != Visibility.Visible) return;
        LinkInputRow.Visibility = Visibility.Collapsed;
        LinkInput.Text = "";
        TextInputChanged?.Invoke(false);
        RebuildLinks();
    }

    /// <summary>Tab ditinggalkan saat mengetik.</summary>
    public void EndInput() => EndLinkInput();

    private void OnLinkKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            EndLinkInput();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (Links.Add(LinkInput.Text) is not null) EndLinkInput();
            else LinkPlaceholder.Text = "Belum ada alamat yang valid, mis. kampus.ac.id";
            e.Handled = true;
        }
    }

    private void OnLinkLostFocus(object sender, KeyboardFocusChangedEventArgs e) => EndLinkInput();

    private void OnLinkTextChanged(object sender, TextChangedEventArgs e) =>
        LinkPlaceholder.Visibility = LinkInput.Text.Trim().Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void Rebuild()
    {
        Tiles.Children.Clear();
        foreach (var app in Launcher.Apps) Tiles.Children.Add(Tile(app));
        if (!Launcher.IsFull) Tiles.Children.Add(AddTile());
        if (Launcher.Apps.Count == 0)
        {
            // Kosong: jelaskan di samping ubin "+".
            Tiles.Children.Add(new TextBlock
            {
                Text = "Simpan aplikasi, file, atau folder yang sering kamu buka. Klik untuk membuka, klik kanan untuk menata.",
                Style = (Style)FindResource("Peeko.Text.Caption"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 300,
                Margin = new Thickness(10, 0, 0, 0),
            });
            Tiles.ItemWidth = double.NaN;
        }
        else
        {
            Tiles.ItemWidth = 72;
        }
    }

    private FrameworkElement Tile(LauncherApp app)
    {
        var icon = new Image
        {
            Width = 32,
            Height = 32,
            Source = Icon(app.Path),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(icon);
        stack.Children.Add(new TextBlock
        {
            Text = app.Name,
            Style = (Style)FindResource("Peeko.Text.Caption"),
            Foreground = B("TextSecondary"),
            FontSize = 10.5,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(2, 6, 2, 0),
        });

        var tile = new Border
        {
            Margin = new Thickness(3),
            CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = app.Path,
            Child = stack,
        };
        tile.MouseEnter += (_, _) => tile.Background = B("SurfaceRaised");
        tile.MouseLeave += (_, _) => tile.Background = Brushes.Transparent;
        tile.MouseLeftButtonUp += (_, _) => Launch(app);
        tile.ContextMenu = Menu(app);
        return tile;
    }

    private FrameworkElement AddTile()
    {
        var plus = new System.Windows.Shapes.Path
        {
            Style = (Style)FindResource("Peeko.IconPath"),
            Data = (Geometry)FindResource("Peeko.Icon.Plus"),
            Stroke = B("TextSecondary"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var circle = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = B("SurfaceRaised"),
            Child = plus,
        };
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(circle);
        stack.Children.Add(new TextBlock
        {
            Text = "Tambah",
            Style = (Style)FindResource("Peeko.Text.Caption"),
            FontSize = 10.5,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(2, 6, 2, 0),
        });
        var tile = new Border
        {
            Margin = new Thickness(3),
            CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = "Pilih aplikasi dari menu Start, atau file/folder apa saja",
            Width = 66,
            Child = stack,
        };
        tile.MouseEnter += (_, _) => tile.Background = B("SurfaceRaised");
        tile.MouseLeave += (_, _) => tile.Background = Brushes.Transparent;
        tile.MouseLeftButtonUp += (_, _) => Pick();
        return tile;
    }

    private ContextMenu Menu(LauncherApp app)
    {
        var menu = new ContextMenu { Style = (Style)FindResource("Peeko.TrayMenu") };
        void Item(string header, Action click, bool enabled = true)
        {
            var mi = new MenuItem { Header = header, Style = (Style)FindResource("Peeko.TrayItem"), IsEnabled = enabled };
            mi.Click += (_, _) => click();
            menu.Items.Add(mi);
        }
        int index = Launcher.Apps.ToList().IndexOf(app);
        Item("Buka lokasi file", () => ShowInExplorer(app.Path), File.Exists(app.Path) || Directory.Exists(app.Path));
        Item("Geser ke kiri", () => Launcher.Move(app, -1), index > 0);
        Item("Geser ke kanan", () => Launcher.Move(app, 1), index < Launcher.Apps.Count - 1);
        Item("Hapus dari daftar", () => Launcher.Remove(app));
        menu.Opened += (_, _) => MenuOpenChanged?.Invoke(true);
        menu.Closed += (_, _) => MenuOpenChanged?.Invoke(false);
        return menu;
    }

    private void Pick()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pilih aplikasi untuk Peeko",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Filter = "Aplikasi dan pintasan|*.lnk;*.exe;*.url;*.appref-ms|Semua file|*.*",
            DereferenceLinks = false,
            Multiselect = true,
        };
        MenuOpenChanged?.Invoke(true);
        try
        {
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            foreach (var path in dialog.FileNames) Launcher.Add(path, ExeDescription(path));
        }
        finally
        {
            MenuOpenChanged?.Invoke(false);
        }
    }

    /// <summary>Keterangan file .exe (mis. "Notepad"), supaya namanya tidak "notepad".</summary>
    private static string? ExeDescription(string path)
    {
        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return string.IsNullOrWhiteSpace(info.FileDescription) ? null : info.FileDescription.Trim();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static void Launch(LauncherApp app)
    {
        try
        {
            Process.Start(new ProcessStartInfo(app.Path) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            MessageBox.Show($"{app.Name} tidak bisa dibuka. Mungkin aplikasinya sudah dihapus atau dipindah.", "Peeko",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private static void ShowInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    // ── Ikon ───────────────────────────────────────────────────────────────

    private ImageSource? Icon(string path)
    {
        if (_icons.TryGetValue(path, out var cached)) return cached;
        ImageSource? image = null;
        var info = new SHFILEINFO();
        // Tanpa SHGFI_LINKOVERLAY: ikon pintasan tampil tanpa panah kecil.
        const uint SHGFI_ICON = 0x100, SHGFI_LARGEICON = 0x0;
        if (SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON) != IntPtr.Zero && info.hIcon != IntPtr.Zero)
        {
            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                image = source;
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        _icons[path] = image;
        return image;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref SHFILEINFO info, uint size, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
