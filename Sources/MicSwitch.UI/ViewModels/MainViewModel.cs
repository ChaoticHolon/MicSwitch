using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicSwitch.Platform;
using MicSwitch.Services;
using MicSwitch.Settings;
using Microsoft.Extensions.Logging;

namespace MicSwitch.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    public const string RepositoryUrl = "https://github.com/ChaoticHolon/MicSwitch";
    private const string ReleasesUrl = RepositoryUrl + "/releases/latest";
    private static readonly Bitmap DefaultMutedIcon = LoadAsset("microphoneDisabled.png");
    private static readonly Bitmap DefaultUnmutedIcon = LoadAsset("microphoneEnabled.png");
    private const float VolumeStep = 0.02f;

    private readonly SettingsService settingsService;
    private readonly IAudioDevices deviceService;
    private readonly IGlobalHotkeys hotkeys;
    private readonly NotificationSounds sounds;
    private readonly IStartupRegistration startup;
    private readonly IUpdateService updates;
    private readonly IDialogService dialogs;
    private readonly ILogger<MainViewModel> logger;
    private readonly IAudioEndpointGroup microphone;
    private readonly IAudioEndpointGroup speakers;
    private readonly DispatcherTimer outputIndicatorTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer volumeRepeatTimer = new() { Interval = TimeSpan.FromMilliseconds(60) };
    private float volumeRepeatStep;
    private readonly List<IDisposable> hotkeyRegistrations = [];
    private bool? lastMute;
    private float? lastSpeakerVolume;
    private bool? lastSpeakerMute;
    private bool isRebinding;

    public MainViewModel(
        SettingsService settingsService,
        IAudioDevices deviceService,
        IGlobalHotkeys hotkeys,
        NotificationSounds sounds,
        IStartupRegistration startup,
        IUpdateService updates,
        IDialogService dialogs,
        PlatformCapabilities capabilities,
        ILogger<MainViewModel> logger)
    {
        this.settingsService = settingsService;
        this.deviceService = deviceService;
        this.hotkeys = hotkeys;
        this.sounds = sounds;
        this.startup = startup;
        this.updates = updates;
        this.dialogs = dialogs;
        Capabilities = capabilities;
        this.logger = logger;

        var s = Settings;
        MainHotkey = Editor("Hotkey", s.Microphone.Hotkey);
        AdvancedHotkeys =
        [
            Editor("Toggle", s.Microphone.ToggleHotkey),
            Editor("Mute", s.Microphone.MuteHotkey),
            Editor("Unmute", s.Microphone.UnmuteHotkey),
            Editor("Push-to-talk", s.Microphone.PushToTalkHotkey),
            Editor("Push-to-mute", s.Microphone.PushToMuteHotkey),
        ];
        SpeakerHotkeys =
        [
            Editor("Toggle mute", s.Output.ToggleHotkey),
            Editor("Mute", s.Output.MuteHotkey),
            Editor("Unmute", s.Output.UnmuteHotkey),
            Editor("Volume up", s.Output.VolumeUpHotkey),
            Editor("Volume down", s.Output.VolumeDownHotkey),
        ];

        microphone = deviceService.CreateGroup(AudioFlow.Capture);
        speakers = deviceService.CreateGroup(AudioFlow.Render);
        volumeRepeatTimer.Tick += (_, _) => speakers.Volume = (speakers.Volume ?? 0) + volumeRepeatStep;
        microphone.StateChanged += (_, _) => OnMicrophoneStateChanged();
        speakers.StateChanged += (_, _) => OnSpeakerStateChanged();
        deviceService.DevicesChanged += (_, _) => RefreshDeviceLists();
        outputIndicatorTimer.Tick += (_, _) =>
        {
            outputIndicatorTimer.Stop();
            ShowOutputIndicator = false;
        };

        RefreshDeviceLists();
        RefreshSounds();
        RefreshIcons();
        Rebind(microphone, s.Microphone.DeviceId);
        Rebind(speakers, s.Output.DeviceId);
        if (MuteRules.InitialMute(s.Microphone.MuteMode, s.Microphone.InitialState) is { } initialMute)
        {
            microphone.Mute = initialMute;
        }

        ApplyMicrophoneVolume();
        ApplyHotkeys();
        lastMute = microphone.Mute;
        OnMicrophoneStateChanged();
    }

    public AppSettings Settings => settingsService.Current;

    public PlatformCapabilities Capabilities { get; }

    public string VersionText { get; } = $"Version {Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)}";


    // ---------------------------------------------------------------- Microphone

    public ObservableCollection<AudioDeviceInfo> Microphones { get; } = [];

    public string SelectedMicrophoneId
    {
        get => Settings.Microphone.DeviceId;
        set
        {
            if (value is null || value == Settings.Microphone.DeviceId)
            {
                return;
            }

            Settings.Microphone.DeviceId = value;
            Rebind(microphone, value);
            ApplyMicrophoneVolume();
            Save();
        }
    }

    public bool? IsMuted => microphone.Mute;

    public bool IsMicrophoneConnected => microphone.IsConnected;

    public string MicrophoneStatus => IsMuted switch
    {
        true => "Microphone muted",
        false => "Microphone live",
        null => "No microphone connected",
    };

    public string MicrophoneStatusDetail =>
        $"{Microphones.FirstOrDefault(m => m.Id == SelectedMicrophoneId)?.Name ?? "Unknown device"} · {MuteModes.First(m => Equals(m.Value, MuteMode)).Name}";

    public string ToggleMuteText => IsMuted == true ? "Unmute" : "Mute";

    public bool IsMutedState => IsMuted == true;

    public bool IsLiveState => IsMuted == false;

    public string StatusGlyph => IsMuted == true ? "\uEC54" : "\uE720";

    public Bitmap CurrentIcon => IsMuted == true ? MutedIcon : UnmutedIcon;

    public IReadOnlyList<Choice> MuteModes { get; } =
    [
        new(MuteMode.ToggleMute, "Toggle", "Each hotkey press switches between muted and live"),
        new(MuteMode.PushToTalk, "Push-to-talk", "Live only while the hotkey is held"),
        new(MuteMode.PushToMute, "Push-to-mute", "Muted only while the hotkey is held"),
    ];

    public MuteMode MuteMode
    {
        get => Settings.Microphone.MuteMode;
        set
        {
            if (SetProperty(Settings.Microphone.MuteMode, value, Settings.Microphone, (m, v) => m.MuteMode = v))
            {
                OnPropertyChanged(nameof(IsToggleMode));
                OnPropertyChanged(nameof(MicrophoneStatusDetail));
                if (MuteRules.InitialMute(value, InitialState) is { } mute)
                {
                    microphone.Mute = mute;
                }

                Save();
            }
        }
    }

    public bool IsToggleMode => MuteMode == MuteMode.ToggleMute;

    public IReadOnlyList<Choice> InitialStates { get; } =
    [
        new(MicrophoneState.Any, "Leave as is"),
        new(MicrophoneState.Mute, "Muted"),
        new(MicrophoneState.Unmute, "Live"),
    ];

    public MicrophoneState InitialState
    {
        get => Settings.Microphone.InitialState;
        set => SetAndSave(Settings.Microphone.InitialState, value, v => Settings.Microphone.InitialState = v);
    }

    public HotkeyEditorViewModel MainHotkey { get; }

    public IReadOnlyList<HotkeyEditorViewModel> AdvancedHotkeys { get; }

    public bool AdvancedHotkeysEnabled
    {
        get => Settings.Microphone.AdvancedHotkeysEnabled;
        set
        {
            if (SetAndSave(Settings.Microphone.AdvancedHotkeysEnabled, value, v => Settings.Microphone.AdvancedHotkeysEnabled = v))
            {
                ApplyHotkeys();
            }
        }
    }

    public bool MicrophoneVolumeControlEnabled
    {
        get => Settings.Microphone.VolumeControlEnabled;
        set
        {
            if (SetAndSave(Settings.Microphone.VolumeControlEnabled, value, v => Settings.Microphone.VolumeControlEnabled = v))
            {
                Settings.Microphone.Volume = value ? microphone.Volume : null;
            }
        }
    }

    /// <summary>Microphone volume in percent.</summary>
    public double MicrophoneVolume
    {
        get => (microphone.Volume ?? 0) * 100;
        set
        {
            microphone.Volume = (float)(value / 100);
            Settings.Microphone.Volume = microphone.Volume;
            Save();
        }
    }

    [RelayCommand]
    private void ToggleMute() => SetMute(!(microphone.Mute ?? false));

    // ---------------------------------------------------------------- Sounds

    public ObservableCollection<Choice> SoundOptions { get; } = [];

    public ObservableCollection<Choice> PlaybackDevices { get; } = [];

    public string? SoundWhenMuted
    {
        get => Settings.Notifications.WhenMuted;
        set => SetAndSave(Settings.Notifications.WhenMuted, value, v => Settings.Notifications.WhenMuted = v);
    }

    public string? SoundWhenUnmuted
    {
        get => Settings.Notifications.WhenUnmuted;
        set => SetAndSave(Settings.Notifications.WhenUnmuted, value, v => Settings.Notifications.WhenUnmuted = v);
    }

    /// <summary>Notification volume in percent.</summary>
    public double NotificationVolume
    {
        get => Settings.Notifications.Volume * 100;
        set => SetAndSave(Settings.Notifications.Volume, (float)(value / 100), v => Settings.Notifications.Volume = v);
    }

    public string? PlaybackDeviceId
    {
        get => Settings.Notifications.OutputDeviceId;
        set => SetAndSave(Settings.Notifications.OutputDeviceId, value, v => Settings.Notifications.OutputDeviceId = v);
    }

    [RelayCommand]
    private Task PlaySound(string? name) => sounds.PlayAsync(name ?? SoundWhenMuted ?? SoundWhenUnmuted, Settings.Notifications.Volume, PlaybackDeviceId);

    [RelayCommand]
    private async Task AddSound()
    {
        var file = await dialogs.PickFileAsync("Add notification sound", "Audio files", ["*.wav", "*.mp3", "*.m4a", "*.wma", "*.aac"]);
        if (file is null)
        {
            return;
        }

        try
        {
            var name = await Task.Run(() => sounds.Add(file));
            RefreshSounds();
            SoundWhenMuted ??= name;
            StatusMessage = $"Added sound \"{name}\".";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            StatusMessage = $"Could not add sound: {ex.Message}";
        }
    }

    // ---------------------------------------------------------------- Overlay

    public IReadOnlyList<Choice> OverlayVisibilityModes { get; } =
    [
        new(OverlayVisibilityMode.Always, "Always"),
        new(OverlayVisibilityMode.WhenMuted, "When muted"),
        new(OverlayVisibilityMode.WhenUnmuted, "When live"),
        new(OverlayVisibilityMode.Never, "Never"),
    ];

    public OverlayVisibilityMode OverlayVisibility
    {
        get => Settings.Overlay.Visibility;
        set
        {
            if (SetAndSave(Settings.Overlay.Visibility, value, v => Settings.Overlay.Visibility = v))
            {
                OnPropertyChanged(nameof(IsOverlayVisible));
                OnPropertyChanged(nameof(IsOverlayEnabled));
            }
        }
    }

    public bool IsOverlayEnabled => OverlayVisibility != OverlayVisibilityMode.Never;

    /// <summary>An unlocked overlay stays visible so it can be positioned.</summary>
    public bool IsOverlayVisible => IsOverlayEnabled && (!IsOverlayLocked || MuteRules.IsOverlayVisible(OverlayVisibility, IsMuted == true));

    public bool IsOverlayLocked
    {
        get => Settings.Overlay.IsLocked;
        set
        {
            if (SetAndSave(Settings.Overlay.IsLocked, value, v => Settings.Overlay.IsLocked = v))
            {
                OnPropertyChanged(nameof(IsOverlayUnlocked));
                OnPropertyChanged(nameof(IsOverlayVisible));
            }
        }
    }

    public bool IsOverlayUnlocked
    {
        get => !IsOverlayLocked;
        set => IsOverlayLocked = !value;
    }

    public double OverlayOpacity
    {
        get => Settings.Overlay.Opacity;
        set => SetAndSave(Settings.Overlay.Opacity, Math.Clamp(value, 0.1, 1), v => Settings.Overlay.Opacity = v);
    }

    [ObservableProperty]
    public partial Bitmap MutedIcon { get; private set; } = DefaultMutedIcon;

    [ObservableProperty]
    public partial Bitmap UnmutedIcon { get; private set; } = DefaultUnmutedIcon;

    [ObservableProperty]
    public partial bool ShowOutputIndicator { get; private set; }

    public string OutputIndicatorGlyph => speakers.Mute == true ? "" : "";

    public string OutputIndicatorText => speakers.Mute == true ? "Muted" : $"{SpeakerVolume:0}%";

    /// <summary>Raised when the overlay should move back to its default position.</summary>
    public event EventHandler? OverlayResetRequested;

    [RelayCommand]
    private void ToggleOverlayLock() => IsOverlayLocked = !IsOverlayLocked;

    [RelayCommand]
    private void ResetOverlayPosition()
    {
        Settings.Overlay.Bounds = null;
        OverlayResetRequested?.Invoke(this, EventArgs.Empty);
        Save();
    }

    [RelayCommand]
    private Task SelectMutedIcon() => SelectIcon("muted", p => Settings.Overlay.MutedIconPath = p);

    [RelayCommand]
    private Task SelectUnmutedIcon() => SelectIcon("unmuted", p => Settings.Overlay.UnmutedIconPath = p);

    [RelayCommand]
    private void ResetIcons()
    {
        Settings.Overlay.MutedIconPath = null;
        Settings.Overlay.UnmutedIconPath = null;
        RefreshIcons();
        Save();
    }

    // ---------------------------------------------------------------- Speakers

    public ObservableCollection<AudioDeviceInfo> Speakers { get; } = [];

    public bool SpeakerControlEnabled
    {
        get => Settings.Output.Enabled;
        set
        {
            if (SetAndSave(Settings.Output.Enabled, value, v => Settings.Output.Enabled = v))
            {
                ApplyHotkeys();
            }
        }
    }

    public string SelectedSpeakerId
    {
        get => Settings.Output.DeviceId;
        set
        {
            if (value is not null && SetAndSave(Settings.Output.DeviceId, value, v => Settings.Output.DeviceId = v))
            {
                Rebind(speakers, value);
            }
        }
    }

    public double SpeakerVolume
    {
        get => (speakers.Volume ?? 0) * 100;
        set => speakers.Volume = (float)(value / 100);
    }

    public IReadOnlyList<HotkeyEditorViewModel> SpeakerHotkeys { get; }

    // ---------------------------------------------------------------- General

    public IReadOnlyList<Choice> Themes { get; } =
    [
        new(AppTheme.System, "Use system setting"),
        new(AppTheme.Light, "Light"),
        new(AppTheme.Dark, "Dark"),
    ];

    public AppTheme Theme
    {
        get => Settings.Window.Theme;
        set
        {
            if (SetAndSave(Settings.Window.Theme, value, v => Settings.Window.Theme = v))
            {
                App.ApplyTheme(value);
            }
        }
    }

    public bool RunAtStartup
    {
        get => startup.IsEnabled;
        set
        {
            try
            {
                startup.SetEnabled(value);
            }
            catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                StatusMessage = ex.Message;
            }

            OnPropertyChanged();
        }
    }

    public bool StartMinimized
    {
        get => Settings.Window.StartMinimized;
        set => SetAndSave(Settings.Window.StartMinimized, value, v => Settings.Window.StartMinimized = v);
    }

    public bool MinimizeOnClose
    {
        get => Settings.Window.MinimizeOnClose;
        set => SetAndSave(Settings.Window.MinimizeOnClose, value, v => Settings.Window.MinimizeOnClose = v);
    }

    public bool CheckForUpdates
    {
        get => Settings.CheckForUpdates;
        set => SetAndSave(Settings.CheckForUpdates, value, v => Settings.CheckForUpdates = v);
    }

    [ObservableProperty]
    public partial string? UpdateVersion { get; private set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [RelayCommand]
    private async Task CheckUpdates()
    {
        if (!updates.IsInstalled)
        {
            StatusMessage = "This copy isn't installed, so it can't update itself. Opening the releases page instead.";
            await dialogs.OpenAsync(ReleasesUrl);
            return;
        }

        try
        {
            UpdateVersion = await updates.CheckAsync();
            StatusMessage = UpdateVersion is null ? "MicSwitch is up to date." : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            StatusMessage = $"Update check failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task InstallUpdate()
    {
        try
        {
            await updates.DownloadAndRestartAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            StatusMessage = $"Update failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private Task OpenDataFolder() => dialogs.OpenAsync(settingsService.Directory);

    [RelayCommand]
    private Task OpenProjectPage() => dialogs.OpenAsync(RepositoryUrl);

    [RelayCommand]
    private void DismissStatus() => StatusMessage = null;

    /// <summary>Runs an update check on startup when enabled; failures are silent.</summary>
    public async Task CheckForUpdatesOnStartupAsync()
    {
        if (!CheckForUpdates || !updates.IsInstalled)
        {
            return;
        }

        try
        {
            UpdateVersion = await updates.CheckAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            LogUpdateCheckFailed(ex);
        }
    }

    public void Dispose()
    {
        hotkeyRegistrations.ForEach(r => r.Dispose());
        microphone.Dispose();
        speakers.Dispose();
    }

    // ---------------------------------------------------------------- Internals

    private HotkeyEditorViewModel Editor(string title, HotkeySettings settings) => new(
        title,
        settings,
        () =>
        {
            ApplyHotkeys();
            Save();
        },
        Capabilities.CanSuppressHotkeys,
        Capabilities.SupportsMouseHotkeys);

    private void ApplyHotkeys()
    {
        hotkeyRegistrations.ForEach(r => r.Dispose());
        hotkeyRegistrations.Clear();
        var mic = Settings.Microphone;
        Register(mic.Hotkey, pressed => SetMute(MuteRules.OnMainHotkey(mic.MuteMode, pressed, microphone.Mute)));
        if (mic.AdvancedHotkeysEnabled)
        {
            Register(mic.ToggleHotkey, pressed => SetMute(pressed ? !(microphone.Mute ?? false) : null));
            Register(mic.MuteHotkey, pressed => SetMute(pressed ? true : null));
            Register(mic.UnmuteHotkey, pressed => SetMute(pressed ? false : null));
            Register(mic.PushToTalkHotkey, pressed => SetMute(!pressed));
            Register(mic.PushToMuteHotkey, pressed => SetMute(pressed));
        }

        var output = Settings.Output;
        if (output.Enabled)
        {
            Register(output.ToggleHotkey, pressed => speakers.Mute = pressed ? !(speakers.Mute ?? false) : null);
            Register(output.MuteHotkey, pressed => speakers.Mute = pressed ? true : null);
            Register(output.UnmuteHotkey, pressed => speakers.Mute = pressed ? false : null);
            Register(output.VolumeUpHotkey, pressed => RepeatVolumeStep(pressed, VolumeStep));
            Register(output.VolumeDownHotkey, pressed => RepeatVolumeStep(pressed, -VolumeStep));
        }

        void Register(HotkeySettings settings, Action<bool> handler) => hotkeyRegistrations.Add(hotkeys.Register(settings, handler));
    }

    private void RepeatVolumeStep(bool pressed, float step)
    {
        volumeRepeatTimer.Stop();
        if (!pressed)
        {
            return;
        }

        volumeRepeatStep = step;
        speakers.Volume = (speakers.Volume ?? 0) + step;
        volumeRepeatTimer.Start();
    }

    private void SetMute(bool? mute)
    {
        if (mute is { } value)
        {
            microphone.Mute = value;
        }
    }

    private void Rebind(IAudioEndpointGroup group, string deviceId)
    {
        isRebinding = true;
        try
        {
            if (group.DeviceId == deviceId)
            {
                group.Rebind();
            }
            else
            {
                group.DeviceId = deviceId;
            }
        }
        finally
        {
            isRebinding = false;
        }
    }

    private void ApplyMicrophoneVolume()
    {
        if (Settings.Microphone is { VolumeControlEnabled: true, Volume: { } volume })
        {
            microphone.Volume = volume;
        }
    }

    private void OnMicrophoneStateChanged()
    {
        var mute = microphone.Mute;
        if (!isRebinding && lastMute is not null && mute is not null && mute != lastMute)
        {
            _ = sounds.PlayAsync(mute.Value ? SoundWhenMuted : SoundWhenUnmuted, Settings.Notifications.Volume, PlaybackDeviceId);
        }

        lastMute = mute ?? lastMute;
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(IsMicrophoneConnected));
        OnPropertyChanged(nameof(MicrophoneStatus));
        OnPropertyChanged(nameof(MicrophoneStatusDetail));
        OnPropertyChanged(nameof(ToggleMuteText));
        OnPropertyChanged(nameof(IsMutedState));
        OnPropertyChanged(nameof(IsLiveState));
        OnPropertyChanged(nameof(StatusGlyph));
        OnPropertyChanged(nameof(CurrentIcon));
        OnPropertyChanged(nameof(IsOverlayVisible));
        OnPropertyChanged(nameof(MicrophoneVolume));
    }

    private void OnSpeakerStateChanged()
    {
        var (volume, mute) = (speakers.Volume, speakers.Mute);
        var changed = volume != lastSpeakerVolume || mute != lastSpeakerMute;
        if (changed && !isRebinding && lastSpeakerVolume is not null && SpeakerControlEnabled)
        {
            ShowOutputIndicator = true;
            outputIndicatorTimer.Stop();
            outputIndicatorTimer.Start();
        }

        (lastSpeakerVolume, lastSpeakerMute) = (volume, mute);
        OnPropertyChanged(nameof(SpeakerVolume));
        OnPropertyChanged(nameof(OutputIndicatorGlyph));
        OnPropertyChanged(nameof(OutputIndicatorText));
    }

    private void RefreshDeviceLists()
    {
        Replace(Microphones, [new(AppSettings.AllDevices, "All microphones"), .. deviceService.GetDevices(AudioFlow.Capture)], SelectedMicrophoneId, nameof(SelectedMicrophoneId));
        var outputs = deviceService.GetDevices(AudioFlow.Render);
        Replace(Speakers, [new(AppSettings.AllDevices, "All speakers"), .. outputs], SelectedSpeakerId, nameof(SelectedSpeakerId));
        var playbackId = PlaybackDeviceId;
        PlaybackDevices.Clear();
        PlaybackDevices.Add(new(null, "Default device"));
        outputs.ToList().ForEach(d => PlaybackDevices.Add(new(d.Id, d.Name)));
        Settings.Notifications.OutputDeviceId = playbackId;
        OnPropertyChanged(nameof(PlaybackDeviceId));
        OnPropertyChanged(nameof(MicrophoneStatusDetail));

        void Replace(ObservableCollection<AudioDeviceInfo> target, AudioDeviceInfo[] items, string selectedId, string selectedProperty)
        {
            target.Clear();
            items.ToList().ForEach(target.Add);
            // Keep a remembered but currently unplugged device selectable so the choice isn't lost.
            if (target.All(d => d.Id != selectedId))
            {
                target.Add(new AudioDeviceInfo(selectedId, "Disconnected device"));
            }

            OnPropertyChanged(selectedProperty);
        }
    }

    private void RefreshSounds()
    {
        var (muted, unmuted) = (SoundWhenMuted, SoundWhenUnmuted);
        SoundOptions.Clear();
        SoundOptions.Add(new(null, "None"));
        sounds.GetNames().ToList().ForEach(n => SoundOptions.Add(new(n, n)));
        (Settings.Notifications.WhenMuted, Settings.Notifications.WhenUnmuted) = (muted, unmuted);
        OnPropertyChanged(nameof(SoundWhenMuted));
        OnPropertyChanged(nameof(SoundWhenUnmuted));
    }

    private void RefreshIcons()
    {
        MutedIcon = LoadImage(Settings.Overlay.MutedIconPath) ?? DefaultMutedIcon;
        UnmutedIcon = LoadImage(Settings.Overlay.UnmutedIconPath) ?? DefaultUnmutedIcon;
        OnPropertyChanged(nameof(CurrentIcon));
    }

    private async Task SelectIcon(string name, Action<string> assign)
    {
        var file = await dialogs.PickFileAsync("Choose an icon", "Images", ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.ico"]);
        if (file is null)
        {
            return;
        }

        if (LoadImage(file) is not { } preview)
        {
            StatusMessage = "That file isn't an image MicSwitch can read.";
            return;
        }

        preview.Dispose();

        // Copy so the icon keeps working if the original file is moved.
        var directory = Path.Combine(settingsService.Directory, "Icons");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, name + Path.GetExtension(file));
        File.Copy(file, target, overwrite: true);
        assign(target);
        RefreshIcons();
        Save();
    }

    private static Bitmap LoadAsset(string name)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://MicSwitch.UI/Assets/{name}"));
        return new Bitmap(stream);
    }

    private static Bitmap? LoadImage(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private void Save() => settingsService.ScheduleSave();

    private bool SetAndSave<T>(T current, T value, Action<T> assign, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return false;
        }

        assign(value);
        OnPropertyChanged(propertyName);
        Save();
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Update check failed")]
    private partial void LogUpdateCheckFailed(Exception exception);
}
