using MicSwitch.Hotkeys;
using MicSwitch.Settings;

namespace MicSwitch.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("micswitch-tests").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new SettingsStore(directory);
        var settings = new AppSettings();
        settings.Microphone.Hotkey.Key = HotkeyGesture.Parse("Ctrl+F2");
        settings.Microphone.MuteMode = MuteMode.PushToTalk;
        settings.Overlay.Bounds = new WindowBounds(10, 20, 120, 120);

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(HotkeyGesture.Parse("Ctrl+F2"), loaded.Microphone.Hotkey.Key);
        Assert.Equal(MuteMode.PushToTalk, loaded.Microphone.MuteMode);
        Assert.Equal(new WindowBounds(10, 20, 120, 120), loaded.Overlay.Bounds);
        Assert.Contains("\"Ctrl+F2\"", File.ReadAllText(store.FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Defaults_ArePushToTalk_NonExclusive_AndNeedSetup()
    {
        var settings = new SettingsStore(directory).Load();

        Assert.Equal(MuteMode.PushToTalk, settings.Microphone.MuteMode);
        Assert.False(settings.Microphone.Hotkey.Suppress);
        Assert.False(settings.SetupCompleted);
        Assert.Equal(0.85, settings.Overlay.Opacity);
    }

    [Fact]
    public void Load_OlderVersionFile_MarksSetupCompleted()
    {
        File.WriteAllText(Path.Combine(directory, SettingsStore.FileName), """{ "version": 2, "microphone": { "muteMode": "ToggleMute" } }""");

        var settings = new SettingsStore(directory).Load();

        Assert.True(settings.SetupCompleted);
        Assert.Equal(AppSettings.CurrentVersion, settings.Version);
        Assert.Equal(MuteMode.ToggleMute, settings.Microphone.MuteMode);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndKeepsCopy()
    {
        var store = new SettingsStore(directory);
        File.WriteAllText(store.FilePath, "{ not json");

        var loaded = store.Load();

        Assert.Equal(MuteMode.PushToTalk, loaded.Microphone.MuteMode);
        Assert.True(File.Exists(store.FilePath + ".corrupt"));
    }

    [Fact]
    public void Load_MigratesLegacyConfig()
    {
        const string legacy = """
        {
          "Version": 1,
          "Items": [
            { "AssemblyName": "MicSwitch", "TypeName": "MicSwitch.Modularity.MicSwitchConfig", "Version": 1,
              "ConfigValue": {
                "MicrophoneLineId": { "LineId": "{0.0.1.00000000}.{abc}", "Name": "Mic" },
                "MinimizeOnClose": false, "StartMinimized": true,
                "MainWindowBounds": "100,50,600,680",
                "Notifications": { "On": "Beep300", "Off": "custom" },
                "NotificationVolume": 0.5 } },
            { "AssemblyName": "MicSwitch", "TypeName": "MicSwitch.Modularity.MicSwitchHotkeyConfig", "Version": 1,
              "ConfigValue": {
                "Hotkey": { "Key": "CONTROL+F1", "AlternativeKey": "MouseXButton1", "Suppress": false, "IgnoreModifiers": false },
                "MuteMode": "PushToTalk", "EnableAdvancedHotkeys": true, "InitialMicrophoneState": "Mute",
                "HotkeyForToggle": { "Key": "F9", "Suppress": true } } },
            { "AssemblyName": "MicSwitch", "TypeName": "MicSwitch.Modularity.MicSwitchOverlayConfig", "Version": 1,
              "ConfigValue": {
                "OverlayBounds": "10, 20, 120, 120", "OverlayOpacity": 0.8,
                "OverlayVisibilityMode": "WhenMuted", "MicrophoneIcon": "AQID" } },
            { "AssemblyName": "MicSwitch", "TypeName": "MicSwitch.Modularity.MicSwitchVolumeControlConfig", "Version": 1,
              "ConfigValue": { "IsEnabled": true, "DeviceId": { "LineId": "all" }, "HotkeyForVolumeUp": { "Key": "CTRL+Up" } } }
          ]
        }
        """;
        Directory.CreateDirectory(Path.Combine(directory, "release"));
        File.WriteAllText(Path.Combine(directory, "release", "config.cfg"), legacy, System.Text.Encoding.Unicode);

        var settings = new SettingsStore(directory).Load();

        Assert.Equal("{0.0.1.00000000}.{abc}", settings.Microphone.DeviceId);
        Assert.Equal(MuteMode.PushToTalk, settings.Microphone.MuteMode);
        Assert.Equal(MicrophoneState.Mute, settings.Microphone.InitialState);
        Assert.Equal(HotkeyGesture.Parse("Ctrl+F1"), settings.Microphone.Hotkey.Key);
        Assert.Equal(MouseHotkey.XButton1, settings.Microphone.Hotkey.AlternativeKey.Mouse);
        Assert.False(settings.Microphone.Hotkey.Suppress);
        Assert.True(settings.SetupCompleted);
        var toggle = Assert.Single(settings.ExtraHotkeys, e => e.Action == HotkeyAction.ToggleMute);
        Assert.True(toggle.IsEnabled);
        Assert.Equal("F9", toggle.Hotkey.Key.Key);
        Assert.Equal("custom", settings.Notifications.WhenMuted);
        Assert.Equal("Beep300", settings.Notifications.WhenUnmuted);
        Assert.Equal(0.5f, settings.Notifications.Volume);
        Assert.True(settings.Window.StartInTray);
        Assert.Equal(new WindowBounds(100, 50, 600, 680), settings.Window.Bounds);
        Assert.Equal(OverlayVisibilityMode.WhenMuted, settings.Overlay.Visibility);
        Assert.Equal(new WindowBounds(10, 20, 120, 120), settings.Overlay.Bounds);
        Assert.Equal(0.8, settings.Overlay.Opacity, 3);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(settings.Overlay.UnmutedIconPath!));
        var volumeUp = Assert.Single(settings.ExtraHotkeys, e => e.Action == HotkeyAction.SpeakerVolumeUp);
        Assert.True(volumeUp.IsEnabled);
        Assert.Equal(HotkeyGesture.Parse("Ctrl+Up"), volumeUp.Hotkey.Key);
        Assert.True(File.Exists(Path.Combine(directory, SettingsStore.FileName)));
    }
}
