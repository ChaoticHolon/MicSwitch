using System.Text.Json;

namespace MicSwitch.Settings;

/// <summary>Loads and atomically saves <see cref="AppSettings"/> as JSON.</summary>
public sealed class SettingsStore(string directory)
{
    public const string FileName = "settings.json";

    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MicSwitch");

    public string Directory { get; } = directory;

    public string FilePath => Path.Combine(Directory, FileName);

    /// <summary>
    /// Reads settings, falling back to a MicSwitch 1.x configuration and then to defaults.
    /// A corrupt file is preserved as <c>settings.json.corrupt</c> rather than overwritten silently.
    /// </summary>
    public AppSettings Load()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                using var stream = File.OpenRead(FilePath);
                return JsonSerializer.Deserialize(stream, SettingsJsonContext.Readable.AppSettings) ?? new AppSettings();
            }
            catch (JsonException)
            {
                File.Copy(FilePath, FilePath + ".corrupt", overwrite: true);
                return new AppSettings();
            }
        }

        var migrated = LegacyConfigMigrator.TryMigrate(Directory);
        if (migrated is not null)
        {
            Save(migrated);
        }

        return migrated ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var temp = FilePath + ".tmp";
        using (var stream = File.Create(temp))
        {
            JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Readable.AppSettings);
        }

        File.Move(temp, FilePath, overwrite: true);
    }

    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, SettingsJsonContext.Readable.AppSettings);

    public static AppSettings Deserialize(string json) => JsonSerializer.Deserialize(json, SettingsJsonContext.Readable.AppSettings) ?? new AppSettings();
}
