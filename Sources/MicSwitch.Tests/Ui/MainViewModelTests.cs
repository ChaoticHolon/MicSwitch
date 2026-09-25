using Avalonia.Headless.XUnit;
using MicSwitch.Hotkeys;
using MicSwitch.Settings;

namespace MicSwitch.Tests.Ui;

public sealed class MainViewModelTests
{
    [AvaloniaFact]
    public void PushToTalk_MutesAtStart_AndHotkeyUnmutesWhileHeld()
    {
        using var harness = new TestHarness(s =>
        {
            s.Microphone.MuteMode = MuteMode.PushToTalk;
            s.Microphone.Hotkey.Key = HotkeyGesture.Parse("F13");
        });
        var hotkey = harness.ViewModel.Settings.Microphone.Hotkey;

        Assert.True(harness.ViewModel.IsMuted);
        harness.Hotkeys.Trigger(hotkey, pressed: true);
        Assert.False(harness.ViewModel.IsMuted);
        harness.Hotkeys.Trigger(hotkey, pressed: false);
        Assert.True(harness.ViewModel.IsMuted);
    }

    [AvaloniaFact]
    public void Toggle_FlipsOnPressOnly()
    {
        using var harness = new TestHarness(s => s.Microphone.Hotkey.Key = HotkeyGesture.Parse("F13"));
        var hotkey = harness.ViewModel.Settings.Microphone.Hotkey;

        harness.Hotkeys.Trigger(hotkey, pressed: true);
        harness.Hotkeys.Trigger(hotkey, pressed: false);
        Assert.True(harness.ViewModel.IsMuted);

        harness.Hotkeys.Trigger(hotkey, pressed: true);
        Assert.False(harness.ViewModel.IsMuted);
    }

    [AvaloniaFact]
    public void AdvancedHotkeys_OnlyActiveWhenEnabled()
    {
        using var harness = new TestHarness(s => s.Microphone.MuteHotkey.Key = HotkeyGesture.Parse("F14"));
        var muteHotkey = harness.ViewModel.Settings.Microphone.MuteHotkey;

        harness.Hotkeys.Trigger(muteHotkey, pressed: true);
        Assert.False(harness.ViewModel.IsMuted);

        harness.ViewModel.AdvancedHotkeysEnabled = true;
        harness.Hotkeys.Trigger(muteHotkey, pressed: true);
        Assert.True(harness.ViewModel.IsMuted);
    }

    [AvaloniaFact]
    public void SpeakerHotkeys_ChangeVolume()
    {
        using var harness = new TestHarness(s =>
        {
            s.Output.Enabled = true;
            s.Output.MuteHotkey.Key = HotkeyGesture.Parse("F15");
        });

        harness.Hotkeys.Trigger(harness.ViewModel.Settings.Output.MuteHotkey, pressed: true);

        Assert.True(harness.Devices.Speaker.Mute);
        Assert.True(harness.ViewModel.ShowOutputIndicator);
        Assert.Equal("Muted", harness.ViewModel.OutputIndicatorText);
    }

    [AvaloniaFact]
    public void Changes_ArePersisted()
    {
        using var harness = new TestHarness();

        harness.ViewModel.MuteMode = MuteMode.PushToMute;
        harness.ViewModel.RunAtStartup = true;
        harness.Settings.Flush();

        Assert.Equal(MuteMode.PushToMute, new SettingsStore(Path.GetDirectoryName(harness.SettingsFile)!).Load().Microphone.MuteMode);
        Assert.True(harness.Startup.IsEnabled);
    }

    [AvaloniaFact]
    public void UnplugAndReplug_KeepsSelectedDeviceSelectable()
    {
        using var harness = new TestHarness(s => s.Microphone.DeviceId = "mic-1");

        harness.Devices.Microphones.Clear();
        harness.Devices.RaiseDevicesChanged();

        Assert.Contains(harness.ViewModel.Microphones, d => d.Id == "mic-1" && d.Name == "Disconnected device");
        Assert.Equal("mic-1", harness.ViewModel.SelectedMicrophoneId);
    }

    [AvaloniaFact]
    public void SoundAndPlaybackDropdowns_HaveMatchingSelections()
    {
        using var harness = new TestHarness(s => s.Notifications.WhenMuted = "beep750");

        Assert.Equal("Beep750", harness.ViewModel.SoundWhenMuted);
        Assert.Contains(harness.ViewModel.SoundOptions, o => Equals(o.Value, harness.ViewModel.SoundWhenMuted));
        Assert.Equal(string.Empty, harness.ViewModel.PlaybackDeviceId);
        Assert.Contains(harness.ViewModel.PlaybackDevices, o => Equals(o.Value, string.Empty) && o.Name == "Default device");

        harness.ViewModel.SoundWhenUnmuted = string.Empty;
        Assert.Null(harness.ViewModel.Settings.Notifications.WhenUnmuted);
    }
}
