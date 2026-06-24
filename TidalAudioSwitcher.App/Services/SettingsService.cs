using System.IO;
using System.Text.Json;
using TidalAudioSwitcher.App;
using TidalAudioSwitcher.App.Models;

namespace TidalAudioSwitcher.App.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TidalAudioSwitcher");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");
    }

    public string SettingsPath => _settingsPath;

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            MigrateLegacySettings(json, settings);
            return settings;
        }
        catch (Exception ex)
        {
            StartupLogger.LogException("Settings load failed", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
        StartupLogger.Log($"Settings saved → {_settingsPath}");
    }

    public void Update(Action<AppSettings> mutate)
    {
        var settings = Load();
        mutate(settings);
        Save(settings);
    }

    private static void MigrateLegacySettings(string json, AppSettings settings)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("compactMode", out _))
            {
                return;
            }

            if (doc.RootElement.TryGetProperty("isCompactView", out var legacyCompact))
            {
                settings.CompactMode = legacyCompact.GetBoolean();
            }
        }
        catch
        {
            // ignore legacy migration errors
        }
    }
}
