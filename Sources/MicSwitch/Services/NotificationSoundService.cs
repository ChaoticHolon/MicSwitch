using System.Runtime.InteropServices;
using MicSwitch.Settings;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MicSwitch.Services;

/// <summary>
/// Plays mute/unmute sounds. Built-in sounds ship next to the executable; user sounds live in
/// <c>%APPDATA%\MicSwitch\Resources\Notifications</c> (the same folder MicSwitch 1.x used).
/// </summary>
public sealed partial class NotificationSoundService(ILogger<NotificationSoundService> logger)
{
    private static readonly string[] Extensions = [".wav", ".mp3"];
    private readonly string builtInDirectory = Path.Combine(AppContext.BaseDirectory, "Resources", "Notifications");
    private readonly string userDirectory = Path.Combine(SettingsStore.DefaultDirectory, "Resources", "Notifications");
    private CancellationTokenSource? current;

    public IReadOnlyList<string> GetSoundNames() =>
        [.. new[] { builtInDirectory, userDirectory }
            .Where(Directory.Exists)
            .SelectMany(Directory.EnumerateFiles)
            .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Converts any Media Foundation-readable file to WAV in the user sound folder and returns its name.</summary>
    public string AddFromFile(string path)
    {
        Directory.CreateDirectory(userDirectory);
        var name = Path.GetFileNameWithoutExtension(path);
        using var reader = new MediaFoundationReader(path);
        WaveFileWriter.CreateWaveFile(Path.Combine(userDirectory, name + ".wav"), reader);
        return name;
    }

    /// <summary>Plays a sound, cancelling any sound that is still playing. Errors are logged, not thrown.</summary>
    public Task PlayAsync(string? name, float volume, string? outputDeviceId)
    {
        var file = FindFile(name);
        if (file is null)
        {
            return Task.CompletedTask;
        }

        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref current, cts)?.Cancel();
        return Task.Run(() => Play(file, volume, outputDeviceId, cts.Token), cts.Token);
    }

    private string? FindFile(string? name) => string.IsNullOrEmpty(name)
        ? null
        : new[] { userDirectory, builtInDirectory }
            .SelectMany(dir => Extensions.Select(ext => Path.Combine(dir, name + ext)))
            .FirstOrDefault(File.Exists);

    private async Task Play(string file, float volume, string? outputDeviceId, CancellationToken token)
    {
        try
        {
            // Runs on a pool (MTA) thread with its own enumerator so COM objects never cross apartments.
            using var enumerator = new MMDeviceEnumerator();
            using var device = outputDeviceId is null
                ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                : enumerator.GetDevice(outputDeviceId);
            using var reader = new AudioFileReader(file) { Volume = Math.Clamp(volume, 0, 1) };
            using var output = new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: true, latency: 100);
            var stopped = new TaskCompletionSource();
            output.PlaybackStopped += (_, _) => stopped.TrySetResult();
            output.Init(reader);
            output.Play();
            await using (token.Register(output.Stop))
            {
                await stopped.Task.ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is COMException or IOException or InvalidOperationException or FormatException)
        {
            LogPlaybackFailed(ex, file);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to play notification {File}")]
    private partial void LogPlaybackFailed(Exception exception, string file);
}
