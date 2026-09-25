using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using MicSwitch.Controls;
using MicSwitch.Hotkeys;
using MicSwitch.Views;

namespace MicSwitch.Tests.Ui;

public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void Window_ShowsStatus_AndMuteButtonToggles()
    {
        using var harness = new TestHarness();
        var window = new MainWindow(harness.ViewModel, harness.Settings);
        window.Show();

        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Microphone live");
        var muteButton = window.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "Mute"));

        muteButton.Command!.Execute(null);

        Assert.True(harness.Devices.Microphone.Mute);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Microphone muted");
        Assert.Equal(["Beep750"], harness.Sounds.Played);
    }

    [AvaloniaFact]
    public void Window_ListsDevicesAndSounds()
    {
        using var harness = new TestHarness();
        var window = new MainWindow(harness.ViewModel, harness.Settings);
        window.Show();

        Assert.Contains(harness.ViewModel.Microphones, d => d.Name == "USB Microphone");
        Assert.Equal(["None", "Beep300", "Beep750"], harness.ViewModel.SoundOptions.Select(o => o.Name));
    }

    [AvaloniaFact]
    public void Window_HasSettingsTabs()
    {
        using var harness = new TestHarness();
        var window = new MainWindow(harness.ViewModel, harness.Settings);
        window.Show();

        var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
        Assert.Equal(["Microphone", "Hotkeys", "Sounds", "Overlay", "Speakers", "General"], tabs.Items.OfType<TabItem>().Select(t => t.Header as string));

        foreach (var tab in tabs.Items.OfType<TabItem>())
        {
            tabs.SelectedItem = tab;
            window.UpdateLayout();
            Assert.NotEmpty(window.GetVisualDescendants().OfType<SettingRow>());
        }
    }

    [AvaloniaFact]
    public void Theme_Switches()
    {
        using var harness = new TestHarness();
        harness.ViewModel.Theme = Settings.AppTheme.Dark;
        Assert.Equal(Avalonia.Styling.ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);
        harness.ViewModel.Theme = Settings.AppTheme.System;
        Assert.Equal(Avalonia.Styling.ThemeVariant.Default, Avalonia.Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void HotkeyBox_CapturesCombination_AndBackspaceClears()
    {
        using var harness = new TestHarness();
        var window = new MainWindow(harness.ViewModel, harness.Settings);
        window.Show();
        var box = window.GetVisualDescendants().OfType<HotkeyBox>().First();

        box.Focus();
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F5, KeyModifiers = KeyModifiers.Control });

        Assert.Equal(HotkeyGesture.Parse("Ctrl+F5"), harness.ViewModel.MainHotkey.Key);
        Assert.Equal("Ctrl+F5", box.Text);

        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Back });

        Assert.True(harness.ViewModel.MainHotkey.Key.IsEmpty);
    }

    [AvaloniaFact]
    public void HotkeyBox_LoneModifier_CommitsOnRelease()
    {
        using var harness = new TestHarness();
        var window = new MainWindow(harness.ViewModel, harness.Settings);
        window.Show();
        var box = window.GetVisualDescendants().OfType<HotkeyBox>().First();

        box.Focus();
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.LeftCtrl, KeyModifiers = KeyModifiers.Control });
        Assert.True(harness.ViewModel.MainHotkey.Key.IsEmpty);
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.LeftCtrl });

        Assert.Equal(new HotkeyGesture(HotkeyModifiers.None, "LeftCtrl"), harness.ViewModel.MainHotkey.Key);
    }

    [AvaloniaFact]
    public void OverlayWindow_Loads()
    {
        using var harness = new TestHarness();
        var overlay = new OverlayWindow(harness.ViewModel, harness.Settings, new FakeWindowInterop());
        overlay.SyncVisibility();

        Assert.True(overlay.IsVisible);
        harness.ViewModel.OverlayVisibility = OverlayVisibilityMode.Never;
        Assert.False(overlay.IsVisible);
    }
}
