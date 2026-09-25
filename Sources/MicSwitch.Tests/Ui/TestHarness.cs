using Avalonia;
using Avalonia.Headless;
using MicSwitch.Platform;
using MicSwitch.Services;
using MicSwitch.Settings;
using MicSwitch.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

[assembly: AvaloniaTestApplication(typeof(MicSwitch.Tests.Ui.TestHarness))]

namespace MicSwitch.Tests.Ui;

/// <summary>Builds the real view model and views on top of fake platform services.</summary>
internal sealed class TestHarness : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("micswitch-ui").FullName;
    private readonly string builtInSounds;

    public TestHarness(Action<AppSettings>? configure = null)
    {
        builtInSounds = Path.Combine(directory, "builtin");
        Directory.CreateDirectory(builtInSounds);
        File.WriteAllBytes(Path.Combine(builtInSounds, "Beep300.wav"), [0]);
        File.WriteAllBytes(Path.Combine(builtInSounds, "Beep750.wav"), [0]);

        var store = new SettingsStore(directory);
        if (configure is not null)
        {
            var initial = new AppSettings();
            configure(initial);
            store.Save(initial);
        }

        Settings = new SettingsService(store);
        ViewModel = new MainViewModel(
            Settings,
            Devices,
            Hotkeys,
            new NotificationSounds(Sounds, builtInSounds, Path.Combine(directory, "user-sounds")),
            Startup,
            new FakeUpdates(),
            Dialogs,
            new PlatformCapabilities { CanSuppressHotkeys = true, SupportsMouseHotkeys = true },
            NullLogger<MainViewModel>.Instance);
    }

    public FakeAudioDevices Devices { get; } = new();

    public FakeHotkeys Hotkeys { get; } = new();

    public FakeSoundPlayer Sounds { get; } = new();

    public FakeStartup Startup { get; } = new();

    public FakeDialogs Dialogs { get; } = new();

    public SettingsService Settings { get; }

    public MainViewModel ViewModel { get; }

    public string SettingsFile => Path.Combine(directory, SettingsStore.FileName);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public void Dispose()
    {
        ViewModel.Dispose();
        Directory.Delete(directory, recursive: true);
    }
}
