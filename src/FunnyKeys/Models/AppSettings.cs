using System.Collections.Generic;

namespace FunnyKeys.Models;

public enum PlaybackMode { RestartSound, AllowOverlap, IgnoreUntilFinished }
public sealed class AppSettings
{
    public bool Enabled { get; set; }
    public string SelectedSound { get; set; } = "builtin:Boing.wav";
    public int Volume { get; set; } = 75;
    public bool RandomMode { get; set; }
    public bool ChaosMode { get; set; }
    public PlaybackMode PlaybackMode { get; set; } = PlaybackMode.AllowOverlap;
    public bool Repeat { get; set; }
    public bool IncludeModifiers { get; set; }
    public int MinimumInterval { get; set; } = 50;
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public List<string> RandomExcluded { get; set; } = new();
    public List<CustomSound> CustomSounds { get; set; } = new();
}
public sealed record CustomSound(string FileName, string Name);

