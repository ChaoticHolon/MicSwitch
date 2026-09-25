using System.Windows.Threading;
using MicSwitch.Settings;

namespace MicSwitch.Services;

/// <summary>Holds the live settings and writes them to disk shortly after the last change.</summary>
public sealed class SettingsService
{
    private readonly SettingsStore store = new(SettingsStore.DefaultDirectory);
    private readonly DispatcherTimer saveTimer;

    public SettingsService()
    {
        Current = store.Load();
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        saveTimer.Tick += (_, _) => Flush();
    }

    public AppSettings Current { get; }

    public string Directory => store.Directory;

    public void ScheduleSave()
    {
        saveTimer.Stop();
        saveTimer.Start();
    }

    public void Flush()
    {
        saveTimer.Stop();
        store.Save(Current);
    }
}
