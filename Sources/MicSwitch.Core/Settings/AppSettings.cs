using System.Text.Json.Serialization;
using MicSwitch.Hotkeys;

namespace MicSwitch.Settings;

public sealed class AppSettings
{
    /// <summary>Device id meaning "every active device of this kind".</summary>
    public const string AllDevices = "all";

    public int Version { get; set; } = 2;

    public MicrophoneSettings Microphone { get; set; } = new();

    public NotificationSettings Notifications { get; set; } = new();

    public OverlaySettings Overlay { get; set; } = new();

    public OutputSettings Output { get; set; } = new();

    public WindowSettings Window { get; set; } = new();

    public bool CheckForUpdates { get; set; } = true;
}

public sealed class HotkeySettings
{
    public HotkeyGesture Key { get; set; } = HotkeyGesture.Empty;

    public HotkeyGesture AlternativeKey { get; set; } = HotkeyGesture.Empty;

    /// <summary>Prevents other applications from receiving the hotkey.</summary>
    public bool Suppress { get; set; } = true;

    public bool IgnoreModifiers { get; set; }

    [JsonIgnore]
    public bool IsEmpty => Key.IsEmpty && AlternativeKey.IsEmpty;
}

public sealed class MicrophoneSettings
{
    public string DeviceId { get; set; } = AppSettings.AllDevices;

    public MuteMode MuteMode { get; set; } = MuteMode.ToggleMute;

    public MicrophoneState InitialState { get; set; } = MicrophoneState.Any;

    public HotkeySettings Hotkey { get; set; } = new();

    public bool AdvancedHotkeysEnabled { get; set; }

    public HotkeySettings ToggleHotkey { get; set; } = new();

    public HotkeySettings MuteHotkey { get; set; } = new();

    public HotkeySettings UnmuteHotkey { get; set; } = new();

    public HotkeySettings PushToTalkHotkey { get; set; } = new();

    public HotkeySettings PushToMuteHotkey { get; set; } = new();

    public bool VolumeControlEnabled { get; set; }

    public float? Volume { get; set; }
}

public sealed class NotificationSettings
{
    public string? WhenMuted { get; set; } = "Beep750";

    public string? WhenUnmuted { get; set; } = "Beep300";

    public float Volume { get; set; } = 1;

    /// <summary>Playback device id; <c>null</c> uses the system default.</summary>
    public string? OutputDeviceId { get; set; }
}

public sealed class OverlaySettings
{
    public OverlayVisibilityMode Visibility { get; set; } = OverlayVisibilityMode.Always;

    public bool IsLocked { get; set; } = true;

    public double Opacity { get; set; } = 1;

    /// <summary>Position in screen pixels and size in device-independent pixels; <c>null</c> uses the default location.</summary>
    public WindowBounds? Bounds { get; set; }

    public string? MutedIconPath { get; set; }

    public string? UnmutedIconPath { get; set; }
}

public sealed class OutputSettings
{
    public bool Enabled { get; set; }

    public string DeviceId { get; set; } = AppSettings.AllDevices;

    public HotkeySettings ToggleHotkey { get; set; } = new();

    public HotkeySettings MuteHotkey { get; set; } = new();

    public HotkeySettings UnmuteHotkey { get; set; } = new();

    public HotkeySettings VolumeUpHotkey { get; set; } = new();

    public HotkeySettings VolumeDownHotkey { get; set; } = new();
}

public sealed class WindowSettings
{
    public WindowBounds? Bounds { get; set; }

    public bool StartMinimized { get; set; }

    public bool MinimizeOnClose { get; set; } = true;

    public AppTheme Theme { get; set; } = AppTheme.System;
}

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public sealed record WindowBounds(double Left, double Top, double Width, double Height);
