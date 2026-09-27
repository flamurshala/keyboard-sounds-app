using FunnyKeys.Helpers;

namespace FunnyKeys.Models;
public sealed class SoundItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public bool IsCustom { get; init; }
    public string Kind => IsCustom ? "YOUR SOUND" : "SYNTH ORIGINAL";
    private bool _randomEnabled = true;
    public bool RandomEnabled { get => _randomEnabled; set => Set(ref _randomEnabled, value); }
    public override string ToString() => Name;
}
