using System.Text.Json;

namespace FilesCombiner.Models;

public class AppSettings
{
    public string? WhitelistPattern { get; set; }
    public string? BlacklistPattern { get; set; }

    private static readonly string SettingsPath = Path.Combine(FileSystem.AppDataDirectory, "settings.json");

    public static async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = await File.ReadAllTextAsync(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Return default settings if loading fails
        }

        return new AppSettings();
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(SettingsPath, json);
        }
        catch
        {
            // Handle save error
        }
    }
}