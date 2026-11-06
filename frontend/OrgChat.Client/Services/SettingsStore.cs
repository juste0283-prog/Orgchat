using System.Text.Json;
using System.Text.Json.Serialization;
using OrgChat.Client.UI;

namespace OrgChat.Client.Services;

/// <summary>Préférences locales du client (langue et thème).</summary>
public sealed class AppSettings
{
    public AppLanguage Language { get; set; } = AppLanguage.French;

    public AppTheme Theme { get; set; } = AppTheme.Dark;
}

/// <summary>Persistance des préférences dans %LOCALAPPDATA%\OrgChat\settings.json.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OrgChat",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is null)
                return new AppSettings();

            if (!Enum.IsDefined(settings.Language))
                settings.Language = AppLanguage.French;
            if (!Enum.IsDefined(settings.Theme))
                settings.Theme = AppTheme.Dark;

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
