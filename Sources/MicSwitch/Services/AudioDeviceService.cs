using System.Runtime.InteropServices;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace MicSwitch.Services;

public sealed record AudioDeviceInfo(string Id, string Name);

/// <summary>Enumerates audio endpoints and reports device changes. All members must be used on the UI thread.</summary>
public sealed class AudioDeviceService : IMMNotificationClient, IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly MMDeviceEnumerator enumerator = new();
    private bool refreshQueued;

    public AudioDeviceService(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        enumerator.RegisterEndpointNotificationCallback(this);
    }

    /// <summary>Raised (on the UI thread, coalesced) when devices are added, removed or change state.</summary>
    public event EventHandler? DevicesChanged;

    public IReadOnlyList<MMDevice> GetActiveDevices(DataFlow flow)
    {
        try
        {
            return [.. enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active)];
        }
        catch (COMException)
        {
            return [];
        }
    }

    public IReadOnlyList<AudioDeviceInfo> GetDeviceInfos(DataFlow flow) =>
        [.. GetActiveDevices(flow).Select(d =>
        {
            using (d)
            {
                return new AudioDeviceInfo(d.ID, d.FriendlyName);
            }
        })];

    public void Dispose()
    {
        enumerator.UnregisterEndpointNotificationCallback(this);
        enumerator.Dispose();
    }

    private void QueueRefresh()
    {
        // Callbacks arrive on a COM worker thread; bursts of them are collapsed into one UI update.
        if (refreshQueued)
        {
            return;
        }

        refreshQueued = true;
        dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            refreshQueued = false;
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState) => QueueRefresh();

    void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId) => QueueRefresh();

    void IMMNotificationClient.OnDeviceRemoved(string deviceId) => QueueRefresh();

    void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
    }

    void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
    }
}
