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
        Rebuild();
    }

    public Launcher Launcher { get; }

    /// <summary>Jumlah baris berubah: notch menyesuaikan tinggi.</summary>
    public new event Action? SizeChanged;

    /// <summary>Menu klik kanan atau dialog pilih aplikasi terbuka: panel ditahan terbuka.</summary>
    public event Action<bool>? MenuOpenChanged;

    public double ContentHeight => Math.Ceiling((Launcher.Apps.Count + (Launcher.IsFull ? 0 : 1)) / (double)PerRow) * RowHeight;

    public void Flush() => _saver.Flush();

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
