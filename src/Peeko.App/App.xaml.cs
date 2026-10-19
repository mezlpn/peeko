using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Net.Http;
using Peeko.App.Lyrics;
using Peeko.App.Music;
using Peeko.Core.Lyrics;
using Peeko.App.Notch;
using Peeko.App.Peeks;
using Peeko.App.Productivity;
using Peeko.App.Settings;
using System.Windows.Media;
using Ellipse = System.Windows.Shapes.Ellipse;
using Peeko.Core.Media;
using Peeko.Core.Notch;
using Peeko.Platform.Media;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Peeko.App;

public partial class App : Application
{
    private const string SingleInstanceName = @"Local\Peeko.SingleInstance";

    private Mutex? _singleInstance;
    private IHost? _host;
    private TaskbarIcon? _tray;

    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Peeko");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        // Jika Serilog sendiri gagal menulis (izin, file terkunci), alasannya dicatat di sini.
        MigrateFromCelah();
        Directory.CreateDirectory(DataDirectory);
        Serilog.Debugging.SelfLog.Enable(msg =>
        {
            try { File.AppendAllText(Path.Combine(DataDirectory, "serilog-selflog.txt"), msg + Environment.NewLine); }
            catch (IOException) { }
        });

        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#endif
            .WriteTo.File(Path.Combine(DataDirectory, "logs", "peeko-.log"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 1_000_000,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 5)
            .CreateLogger();

        DispatcherUnhandledException += OnUnhandledException;
        // Exception di thread latar (timer, COM, hook) mematikan proses tanpa jejak; catat dulu.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "Exception tak tertangani di thread latar");
            Log.CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Task gagal tanpa ditangani");
            args.SetObserved();
        };
        SessionEnding += (_, args) => Log.Information("Windows sign out/shutdown: {Reason}", args.ReasonSessionEnding);
        Log.Information("Peeko {Version} mulai (PID {Pid})", typeof(App).Assembly.GetName().Version, Environment.ProcessId);

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(sp => new NotchStateMachine(
            sp.GetRequiredService<TimeProvider>(),
            action => Dispatcher.BeginInvoke(action)));
        builder.Services.AddSingleton(Dispatcher);
        builder.Services.AddSingleton<IMediaSource, GsmtcMediaSource>();
        builder.Services.AddSingleton<MusicController>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<MusicController>().ViewModel);
        builder.Services.AddSingleton(sp =>
        {
            // LRCLIB meminta User-Agent yang menyebut nama dan versi aplikasi.
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"Peeko/{version} (aplikasi notch Windows)");
            return new LyricsService(http, Path.Combine(DataDirectory, "lyrics"), sp.GetRequiredService<TimeProvider>());
        });
        builder.Services.AddSingleton<LyricsController>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<LyricsController>().ViewModel);
        builder.Services.AddSingleton<SystemPeekController>();
        builder.Services.AddSingleton<FocusController>();
        builder.Services.AddSingleton<WaterController>();
        builder.Services.AddSingleton<ListsController>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<NotchWindow>();
        builder.Services.AddSingleton<NotchController>();
        _host = builder.Build();

        var controller = _host.Services.GetRequiredService<NotchController>();
        controller.OpenSettingsRequested += OpenSettings;
        controller.Start();
        CreateTray();

        if (e.Args.Contains("--settings")) OpenSettings();

        // Alat pengembang: putar semua contoh peek bergantian (dipakai untuk mengecek tampilan).
        if (e.Args.Contains("--demo-peeks"))
        {
            var demo = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            demo.Tick += (_, _) => controller.ShowTestPeek();
            demo.Start();
        }
    }

    /// <summary>
    /// Aplikasi ini dulu bernama "Celah". Pindahkan datanya (pengaturan, to-do, rak, timer, cache
    /// lirik, log) ke folder baru sekali saja, dan hapus entri startup lamanya. Status startup tetap
    /// diambil dari settings.json, jadi entri "Peeko" dibuat ulang saat pengaturan diterapkan.
    /// </summary>
    private static void MigrateFromCelah()
    {
        var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Celah");
        try
        {
            if (Directory.Exists(legacy) && !Directory.Exists(DataDirectory))
                Directory.Move(legacy, DataDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Folder lama sedang dipakai (mis. Celah lama masih berjalan): mulai dengan data baru.
        }
        try
        {
            Platform.Display.StartupRegistration.RemoveLegacy("Celah");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
        }
    }

    private SettingsWindow? _settingsWindow;
    private readonly DispatcherTimer _resumeTimer = new();

    /// <summary>Satu jendela Pengaturan saja; jika sudah terbuka, bawa ke depan.</summary>
    private void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_host!.Services.GetRequiredService<SettingsViewModel>());
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>Menu tray (Settings.dc.html): status, Pengaturan, Sembunyikan selama…, Keluar.</summary>
    private void CreateTray()
    {
        var controller = _host!.Services.GetRequiredService<NotchController>();
        var settingsService = _host.Services.GetRequiredService<SettingsService>();
        var itemStyle = (Style)Resources["Peeko.TrayItem"];

        MenuItem Item(string header, Action? onClick = null)
        {
            var item = new MenuItem { Header = header, Style = itemStyle };
            if (onClick is not null) item.Click += (_, _) => onClick();
            return item;
        }

        // Baris status: "Peeko aktif · n modul berjalan" atau "disembunyikan sampai …".
        var statusText = new TextBlock { FontSize = 11, Foreground = (Brush)Resources["Peeko.Brush.TextSecondary"], VerticalAlignment = VerticalAlignment.Center };
        var statusDot = new Ellipse { Width = 6, Height = 6, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        var status = new MenuItem
        {
            Style = itemStyle,
            IsEnabled = false,
            Header = new StackPanel { Orientation = Orientation.Horizontal, Children = { statusDot, statusText } },
        };

        string? hiddenUntil = null;
        void RefreshStatus()
        {
            var s = settingsService.Current;
            int modules = new[] { s.MusicEnabled, s.LyricsEnabled, s.FocusEnabled, s.WaterEnabled, s.TodoEnabled, s.ShelfEnabled }.Count(x => x);
            statusDot.Fill = (Brush)Resources[hiddenUntil is null ? "Peeko.Brush.Accent" : "Peeko.Brush.TextTertiary"];
            statusText.Text = hiddenUntil is null ? $"Peeko aktif · {modules} modul berjalan" : $"Disembunyikan {hiddenUntil}";
        }

        var hide = Item("Sembunyikan selama");
        var show = Item("Tampilkan lagi");

        void Suspend(TimeSpan? duration, string label)
        {
            controller.SetSuspended(true);
            hiddenUntil = label;
            _resumeTimer.Stop();
            if (duration is { } d)
            {
                _resumeTimer.Interval = d;
                _resumeTimer.Start();
            }
            show.Visibility = Visibility.Visible;
            RefreshStatus();
        }
        void Resume()
        {
            _resumeTimer.Stop();
            controller.SetSuspended(false);
            hiddenUntil = null;
            show.Visibility = Visibility.Collapsed;
            RefreshStatus();
        }
        _resumeTimer.Tick += (_, _) => Resume();
        show.Click += (_, _) => Resume();
        show.Visibility = Visibility.Collapsed;

        hide.Items.Add(Item("30 menit", () => Suspend(TimeSpan.FromMinutes(30), $"sampai {DateTime.Now.AddMinutes(30):HH:mm}")));
        hide.Items.Add(Item("1 jam", () => Suspend(TimeSpan.FromHours(1), $"sampai {DateTime.Now.AddHours(1):HH:mm}")));
        hide.Items.Add(Item("Sampai besok pagi", () =>
        {
            var morning = DateTime.Today.AddDays(1).AddHours(7);
            Suspend(morning - DateTime.Now, "sampai besok pagi");
        }));
        hide.Items.Add(Item("Sampai dinyalakan lagi", () => Suspend(null, "sampai dinyalakan lagi")));
        foreach (MenuItem sub in hide.Items) sub.Style = itemStyle;

        var menu = new ContextMenu { Style = (Style)Resources["Peeko.TrayMenu"] };
        menu.Items.Add(status);
        menu.Items.Add(new Separator { Style = (Style)Resources["Peeko.TraySeparator"] });
        menu.Items.Add(Item("Pengaturan", OpenSettings));
        menu.Items.Add(hide);
        menu.Items.Add(show);
#if DEBUG
        menu.Items.Add(Item("Uji peek", controller.ShowTestPeek));
#endif
        menu.Items.Add(new Separator { Style = (Style)Resources["Peeko.TraySeparator"] });
        menu.Items.Add(Item("Keluar", Shutdown));
        menu.Opened += (_, _) => RefreshStatus();

        _tray = new TaskbarIcon
        {
            ToolTipText = "Peeko",
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/peeko.ico")),
            ContextMenu = menu,
            NoLeftClickDelay = true,
        };
        // Klik kiri ikon tray: buka Pengaturan.
        _tray.TrayLeftMouseUp += (_, _) => OpenSettings();
        // EfficiencyMode (EcoQoS) bisa membuat animasi tersendat, jadi dimatikan.
        _tray.ForceCreate(enablesEfficiencyMode: false);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Exception tak tertangani di thread UI");
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Peeko keluar (kode {Code})", e.ApplicationExitCode);
        _tray?.Dispose();
        _host?.Services.GetService<NotchController>()?.Dispose();
        _host?.Dispose();
        Log.CloseAndFlush();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
