using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Celah.App.Music;
using Celah.App.Notch;
using Celah.Core.Media;
using Celah.Core.Notch;
using Celah.Platform.Media;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Celah.App;

public partial class App : Application
{
    private const string SingleInstanceName = @"Local\Celah.SingleInstance";

    private Mutex? _singleInstance;
    private IHost? _host;
    private TaskbarIcon? _tray;

    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Celah");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#endif
            .WriteTo.File(Path.Combine(DataDirectory, "logs", "celah-.log"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 1_000_000,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 5)
            .CreateLogger();

        DispatcherUnhandledException += OnUnhandledException;

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
        builder.Services.AddSingleton<NotchWindow>();
        builder.Services.AddSingleton<NotchController>();
        _host = builder.Build();

        _host.Services.GetRequiredService<NotchController>().Start();
        CreateTray();
    }

    private void CreateTray()
    {
        var controller = _host!.Services.GetRequiredService<NotchController>();

        var hide = new MenuItem { Header = "Sembunyikan sementara", IsCheckable = true };
        hide.Click += (_, _) => controller.SetSuspended(hide.IsChecked);

        var testPeek = new MenuItem { Header = "Uji peek" };
        testPeek.Click += (_, _) => controller.ShowTestPeek();

        var settings = new MenuItem { Header = "Pengaturan", IsEnabled = false };

        var exit = new MenuItem { Header = "Keluar" };
        exit.Click += (_, _) => Shutdown();

        _tray = new TaskbarIcon
        {
            ToolTipText = "Celah",
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/celah.ico")),
            ContextMenu = new ContextMenu { Items = { settings, hide, testPeek, new Separator(), exit } },
            NoLeftClickDelay = true,
        };
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
        _tray?.Dispose();
        _host?.Services.GetService<NotchController>()?.Dispose();
        _host?.Dispose();
        Log.CloseAndFlush();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
