using System.IO;
using System.Text.Json;
using TinyIcon.Models;

namespace TinyIcon.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON in the user's roaming profile.
/// Persistence is best-effort: any I/O or parse failure falls back to defaults silently.
/// </summary>
public static class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private static AppSettings? _current;

    /// <summary>The in-memory settings, loaded from disk on first access.</summary>
    public static AppSettings Current => _current ??= LoadFrom(GetSettingsPath());

    /// <summary>Loads the settings from disk, replacing <see cref="Current"/>.</summary>
    public static void Load() => _current = LoadFrom(GetSettingsPath());

    /// <summary>Writes <see cref="Current"/> to disk.</summary>
    public static void Save() => SaveTo(GetSettingsPath(), Current);

    internal static AppSettings LoadFrom(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    internal static void SaveTo(string path, AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, SerializerOptions));
        }
        catch
        {
            // Losing the saved state must never break the application.
        }
    }

    private static string GetSettingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TinyIcon", "Settings.json");
}
