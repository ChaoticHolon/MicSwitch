using System.Windows;
using System.Windows.Threading;
using MicSwitch.Controls;
using MicSwitch.Services;
using MicSwitch.Settings;
using MicSwitch.ViewModels;
using MicSwitch.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MicSwitch;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Disposed in OnExit; Application has no Dispose.")]
public partial class App : Application
{
    private readonly bool isAutostart;
    private readonly EventWaitHandle showSignal;
    private IHost? host;
    private TrayIcon? trayIcon;
    private RegisteredWaitHandle? showSignalRegistration;

    public App(bool isAutostart, EventWaitHandle showSignal)
    {
        this.isAutostart = isAutostart;
        this.showSignal = showSignal;
    }

    public static void ApplyTheme(AppTheme theme)
    {
        Current.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Logging.AddDebug().AddProvider(new FileLoggerProvider(Path.Combine(SettingsStore.DefaultDirectory, "logs")));
        builder.Services
            .AddSingleton(Dispatcher)
            .AddSingleton<SettingsService>()
            .AddSingleton<AudioDeviceService>()
            .AddSingleton<GlobalHotkeyService>()
            .AddSingleton<NotificationSoundService>()
            .AddSingleton<StartupService>()
            .AddSingleton<UpdateService>()
            .AddSingleton<MainViewModel>()
            .AddSingleton<MainWindow>()
            .AddSingleton<OverlayWindow>();
        host = builder.Build();

        var services = host.Services;
        var logger = services.GetRequiredService<ILogger<App>>();
        DispatcherUnhandledException += (_, args) =>
        {
            LogUnhandled(logger, args.Exception);
            args.Handled = true;
        };

        var settings = services.GetRequiredService<SettingsService>();
        ApplyTheme(settings.Current.Window.Theme);

        var hotkeys = services.GetRequiredService<GlobalHotkeyService>();
        HotkeyBox.CapturingChanged += (_, capturing) => hotkeys.IsPaused = capturing;

        var viewModel = services.GetRequiredService<MainViewModel>();
        var mainWindow = services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        trayIcon = new TrayIcon(viewModel, mainWindow.ShowAndActivate, ExitApplication);
        services.GetRequiredService<OverlayWindow>().SyncVisibility();

        // A second launch signals this instance to bring its window forward.
        showSignalRegistration = ThreadPool.RegisterWaitForSingleObject(
            showSignal, (_, _) => Dispatcher.BeginInvoke(mainWindow.ShowAndActivate), null, Timeout.Infinite, executeOnlyOnce: false);

        if (!(isAutostart || settings.Current.Window.StartMinimized))
        {
            mainWindow.ShowAndActivate();
        }

        await viewModel.CheckForUpdatesOnStartupAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        showSignalRegistration?.Unregister(null);
        trayIcon?.Dispose();
        if (host is not null)
        {
            host.Services.GetRequiredService<SettingsService>().Flush();
            host.Dispose();
        }

        base.OnExit(e);
    }

    private void ExitApplication()
    {
        if (MainWindow is MainWindow window)
        {
            window.IsExiting = true;
            window.Close();
        }

        Shutdown();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled UI exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
