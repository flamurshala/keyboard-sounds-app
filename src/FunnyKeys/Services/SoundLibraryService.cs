using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FunnyKeys.Models;

namespace FunnyKeys.Services;
public sealed class SoundLibraryService
{
    public string CustomDirectory { get; }
    public static readonly string[] Extensions = { ".wav", ".mp3", ".m4a", ".aac", ".flac" };
    public SoundLibraryService(string root) => CustomDirectory = Path.Combine(root, "Sounds");
    public IEnumerable<SoundItem> Load(AppSettings settings)
    {
        string builtins = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");
        if (Directory.Exists(builtins))
            foreach (var path in Directory.EnumerateFiles(builtins).Where(IsSupported).OrderBy(Path.GetFileName))
                yield return Item("builtin:" + Path.GetFileName(path), Path.GetFileNameWithoutExtension(path), path, false, settings);
        foreach (var custom in settings.CustomSounds.Where(c => c != null && IsSafeName(c.FileName)).DistinctBy(c => c.FileName))
            yield return Item("custom:" + custom.FileName, custom.Name, Path.Combine(CustomDirectory, custom.FileName), true, settings);
    }
    private static SoundItem Item(string id, string name, string path, bool custom, AppSettings settings) => new()
    { Id = id, Name = name, Path = path, IsCustom = custom, RandomEnabled = !settings.RandomExcluded.Contains(id) };
    public static bool IsSupported(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());
    private static bool IsSafeName(string name) => !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && Path.GetFileName(name) == name && IsSupported(name);
    public async Task<SoundItem> ImportAsync(string source)
    {
        if (!IsSupported(source)) throw new InvalidDataException("Choose WAV or MP3 audio (or a supported Windows AAC/M4A/FLAC file).");
        return await Task.Run(() =>
        {
            if (new FileInfo(source).Length > 20 * 1024 * 1024) throw new InvalidDataException("Choose a file smaller than 20 MB.");
            Directory.CreateDirectory(CustomDirectory);
            string name = Guid.NewGuid().ToString("N") + Path.GetExtension(source).ToLowerInvariant();
            string destination = Path.Combine(CustomDirectory, name);
            try
            {
                File.Copy(source, destination);
                AudioPlaybackService.Decode(destination); // Validate the exact private copy before adding it to settings.
                return new SoundItem { Id = "custom:" + name, Name = Path.GetFileNameWithoutExtension(source), Path = destination, IsCustom = true };
            }
            catch { if (File.Exists(destination)) File.Delete(destination); throw; }
        });
    }
    public void Remove(SoundItem sound)
    {
        if (!sound.IsCustom || Path.GetDirectoryName(Path.GetFullPath(sound.Path)) != Path.GetFullPath(CustomDirectory)) throw new InvalidOperationException("Built-in sounds cannot be removed.");
        File.Delete(sound.Path);
    }
}
