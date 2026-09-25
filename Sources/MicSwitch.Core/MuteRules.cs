namespace MicSwitch;

public enum MuteMode
{
    ToggleMute,
    PushToTalk,
    PushToMute,
}

public enum MicrophoneState
{
    Any,
    Mute,
    Unmute,
}

public enum OverlayVisibilityMode
{
    Always,
    Never,
    WhenMuted,
    WhenUnmuted,
}

/// <summary>Pure decision logic for how hotkeys and settings affect the microphone.</summary>
public static class MuteRules
{
    /// <summary>Mute state to apply when the app starts or the mode changes; <c>null</c> means leave it alone.</summary>
    public static bool? InitialMute(MuteMode mode, MicrophoneState initialState) => mode switch
    {
        MuteMode.PushToTalk => true,
        MuteMode.PushToMute => false,
        _ => initialState switch
        {
            MicrophoneState.Mute => true,
            MicrophoneState.Unmute => false,
            _ => null,
        },
    };

    /// <summary>Mute state after the main hotkey is pressed or released; <c>null</c> means no change.</summary>
    public static bool? OnMainHotkey(MuteMode mode, bool isPressed, bool? currentMute) => mode switch
    {
        MuteMode.PushToTalk => !isPressed,
        MuteMode.PushToMute => isPressed,
        _ => isPressed ? !(currentMute ?? false) : null,
    };

    public static bool IsOverlayVisible(OverlayVisibilityMode mode, bool isMuted) => mode switch
    {
        OverlayVisibilityMode.Always => true,
        OverlayVisibilityMode.WhenMuted => isMuted,
        OverlayVisibilityMode.WhenUnmuted => !isMuted,
        _ => false,
    };
}
