using System.Runtime.InteropServices;
using System.Windows.Threading;
using MicSwitch.Settings;
using NAudio.CoreAudioApi;

namespace MicSwitch.Services;

/// <summary>
/// Mute/volume control over either one endpoint or all active endpoints of a data flow
/// (<see cref="AppSettings.AllDevices"/>). In "all" mode newly connected devices inherit the current mute state.
/// Must be used on the UI thread.
/// </summary>
public sealed class AudioEndpointGroup : IDisposable
{
    private readonly AudioDeviceService devices;
    private readonly DataFlow flow;
    private readonly Dispatcher dispatcher;
    private List<MMDevice> endpoints = [];
    private string deviceId = AppSettings.AllDevices;
    private bool? lastMute;

    public AudioEndpointGroup(AudioDeviceService devices, DataFlow flow, Dispatcher dispatcher)
    {
        this.devices = devices;
        this.flow = flow;
        this.dispatcher = dispatcher;
        devices.DevicesChanged += OnDevicesChanged;
    }

    /// <summary>Raised on the UI thread whenever mute or volume changes, from this app or externally.</summary>
    public event EventHandler? StateChanged;

    public string DeviceId
    {
        get => deviceId;
        set
        {
            if (deviceId != value)
            {
                deviceId = value;
                Rebind();
            }
        }
    }

    public bool IsConnected => endpoints.Count > 0;

    /// <summary><c>true</c> only when every controlled endpoint is muted; <c>null</c> when nothing is connected.</summary>
    public bool? Mute
    {
        get => IsConnected ? endpoints.All(e => Try(() => e.AudioEndpointVolume.Mute, false)) : null;
        set
        {
            if (value is not { } mute)
            {
                return;
            }

            lastMute = mute;
            endpoints.ForEach(e => Try(() => e.AudioEndpointVolume.Mute = mute));
        }
    }

    /// <summary>Scalar volume 0..1 of the first endpoint; setting it applies to all.</summary>
    public float? Volume
    {
        get => endpoints.Count > 0 ? Try(() => (float?)endpoints[0].AudioEndpointVolume.MasterVolumeLevelScalar, null) : null;
        set
        {
            if (value is { } volume)
            {
                endpoints.ForEach(e => Try(() => e.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(volume, 0, 1)));
            }
        }
    }

    public void Rebind()
    {
        Release();
        var active = devices.GetActiveDevices(flow);
        endpoints = [.. deviceId == AppSettings.AllDevices ? active : active.Where(d => d.ID == deviceId)];
        foreach (var endpoint in endpoints)
        {
            Try(() => endpoint.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification);
            if (deviceId == AppSettings.AllDevices && lastMute is { } mute)
            {
                Try(() => endpoint.AudioEndpointVolume.Mute = mute);
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        devices.DevicesChanged -= OnDevicesChanged;
        Release();
    }

    private void OnDevicesChanged(object? sender, EventArgs e) => Rebind();

    // Arrives on a COM worker thread.
    private void OnVolumeNotification(AudioVolumeNotificationData data) =>
        dispatcher.BeginInvoke(() => StateChanged?.Invoke(this, EventArgs.Empty));

    private void Release()
    {
        foreach (var endpoint in endpoints)
        {
            Try(() => endpoint.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification);
            endpoint.Dispose();
        }

        endpoints = [];
    }

    // Devices can disappear at any moment; COM calls on a removed device fail and are ignored.
    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch (COMException)
        {
        }
    }

    private static T Try<T>(Func<T> func, T fallback)
    {
        try
        {
            return func();
        }
        catch (COMException)
        {
            return fallback;
        }
    }
}
