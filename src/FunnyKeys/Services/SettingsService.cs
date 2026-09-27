using System;
using System.IO;
using System.Text.Json;
using FunnyKeys.Models;

namespace FunnyKeys.Services;
public sealed class SettingsService
{
    public string Root { get; }
    private string FilePath => Path.Combine(Root, "settings.json");
    public string? Warning { get; private set; }
    public SettingsService(string? root = null) => Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FunnyKeys");
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? throw new JsonException();
            settings.Volume = Math.Clamp(settings.Volume, 0, 100);
            settings.MinimumInterval = Math.Clamp(settings.MinimumInterval, 0, 500);
            if (!Enum.IsDefined(settings.PlaybackMode)) settings.PlaybackMode = PlaybackMode.RestartSound;
            settings.CustomSounds ??= new(); settings.RandomExcluded ??= new();
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Warning = "Settings could not be read. Safe defaults have been restored.";
            return new();
        }
    }
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Root);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
}
