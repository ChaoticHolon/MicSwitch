using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using MicSwitch.Controls;
using MicSwitch.Platform;
using MicSwitch.Services;
using MicSwitch.Settings;
using MicSwitch.ViewModels;
using MicSwitch.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MicSwitch;

/// <summary>How a platform head (the executable) configures the shared app.</summary>
public sealed class AppOptions
{
    /// <summary>Registers the platform's implementations of the MicSwitch.Platform contracts.</summary>
    public required Action<IServiceCollection> RegisterPlatform { get; init; }

    /// <summary>Started from the OS at sign-in: don't show the window.</summary>
    public bool IsAutostart { get; init; }

    /// <summary>Receives a callback to invoke when another instance asks this one to show itself.</summary>
    public Action<Action>? RegisterShowRequest { get; init; }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Disposed on lifetime exit; Application has no Dispose.")]
public partial class App : Application
{
    private readonly AppOptions? options;
    private IHost? host;
    private TrayIcon? trayIcon;

    /// <summary>Used by the XAML previewer and headless tests; nothing is composed.</summary>
    public App()
    {
    }

    public App(AppOptions options) => this.options = options;

    public static void ApplyTheme(AppTheme theme)
    {
        if (Current is { } app)
        {
            app.RequestedThemeVariant = theme switch
            {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }

    /// <summary>Registers the platform-independent services shared by every platform head.</summary>
    public static void RegisterCommonServices(IServiceCollection services, string settingsDirectory)
    {
        services
            .AddSingleton(new SettingsService(new SettingsStore(settingsDirectory)))
            .AddSingleton<IUiDispatcher, AvaloniaDispatcher>()
            .AddSingleton<IDialogService, DialogService>()
            .AddSingleton(sp => new NotificationSounds(
                sp.GetRequiredService<ISoundPlayer>(),
                Path.Combine(AppContext.BaseDirectory, "Resources", "Notifications"),
                Path.Combine(settingsDirectory, "Resources", "Notifications")))
            .AddSingleton<MainViewModel>()
            .AddSingleton<MainWindow>()
            .AddSingleton<OverlayWindow>();
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        if (options is null || ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Logging.AddDebug().AddProvider(new FileLoggerProvider(Path.Combine(SettingsStore.DefaultDirectory, "logs")));
        RegisterCommonServices(builder.Services, SettingsStore.DefaultDirectory);
        options.RegisterPlatform(builder.Services);
        host = builder.Build();

        var services = host.Services;
        var settings = services.GetRequiredService<SettingsService>();
        ApplyTheme(settings.Current.Window.Theme);

        var hotkeys = services.GetRequiredService<IGlobalHotkeys>();
        HotkeyBox.CapturingChanged += (_, capturing) => hotkeys.IsPaused = capturing;

        var viewModel = services.GetRequiredService<MainViewModel>();
        var mainWindow = services.GetRequiredService<MainWindow>();
        desktop.MainWindow = mainWindow;
        trayIcon = new TrayIcon(this, viewModel, mainWindow.ShowAndActivate, () => Exit(desktop, mainWindow));
        services.GetRequiredService<OverlayWindow>().SyncVisibility();
        options.RegisterShowRequest?.Invoke(mainWindow.ShowAndActivate);
        desktop.Exit += (_, _) =>
        {
            trayIcon.Dispose();
            settings.Flush();
            host.Dispose();
        };

        if (!(options.IsAutostart || settings.Current.Window.StartMinimized))
        {
            mainWindow.ShowAndActivate();
        }

        await viewModel.CheckForUpdatesOnStartupAsync();
    }

    private static void Exit(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window)
    {
        window.IsExiting = true;
        window.Close();
        desktop.Shutdown();
    }
}
