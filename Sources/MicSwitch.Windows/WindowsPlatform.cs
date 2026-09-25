using MicSwitch.Platform;

namespace MicSwitch.Windows;

public static class WindowsPlatform
{
    public static PlatformCapabilities Capabilities { get; } = new()
    {
        CanSuppressHotkeys = true,
        SupportsMouseHotkeys = true,
        LimitationNotice = StartupService.IsElevated
            ? null
            : "MicSwitch isn't running as administrator. Hotkeys won't work while an elevated app or game is focused.",
    };
}
