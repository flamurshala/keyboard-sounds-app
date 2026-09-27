using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using FunnyKeys.Helpers;
using FunnyKeys.Models;
using FunnyKeys.Services;
using Microsoft.Win32;

namespace FunnyKeys.ViewModels;
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private sealed record Routing(string? Selected, string[] Random, bool RandomMode, bool Chaos, PlaybackMode Playback, int Interval);
    private readonly SettingsService _store;
    private readonly AppSettings _settings;
    private readonly SoundLibraryService _library;
    private readonly AudioPlaybackService _audio = new();
    private readonly GlobalKeyboardHookService _hook;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DispatcherTimer _pulseTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private Routing _routing = new(null, Array.Empty<string>(), false, false, PlaybackMode.RestartSound, 50);
    private long _lastPress;
    private int _pulse;
    private bool _busy, _disposed;
    private volatile bool _diagnosing;
    private SoundItem? _selected;
    private string _message = "Ready when you are. Pick a sound, then enable your keyboard.";
    private string _activity = "Try a key in any app";
    public ObservableCollection<SoundItem> Sounds { get; } = new();
    public string[] PlaybackOptions { get; } = { "Restart Sound", "Allow Overlap", "Ignore Until Finished" };
    public string[] RepeatOptions { get; } = { "Once per physical press", "Continuously while held" };
    public RelayCommand ToggleCommand { get; }
    public RelayCommand PreviewCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand ExitCommand { get; }
    public RelayCommand DismissCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand AudioDiagnosticsCommand { get; }
    public RelayCommand SinglePressCommand { get; }
    public string RepeatNotice => RepeatIndex == 1 && PlaybackIndex == 0
        ? "Continuous repeat + Restart replays the beginning while a key is held. Choose once per press for one clean trigger, or Ignore Until Finished to hear the full sample."
        : "Restart intentionally starts over on each new press. Ignore Until Finished lets the whole sample play.";
    public bool Busy { get => _busy; private set { Set(ref _busy, value); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } }
    public string Message { get => _message; set => Set(ref _message, value); }
    public string Activity { get => _activity; private set => Set(ref _activity, value); }
    public string LibraryCount => $"{Sounds.Count:00} SOUNDS";
    public bool HotkeyAvailable => _hook.HotkeyAvailable;
    public string HotkeyLabel => HotkeyAvailable ? "Ctrl + Shift + F12" : "Shortcut in use by another app";
    public string Status => Enabled ? "ACTIVE" : "DISABLED";
    public string MasterLabel => Enabled ? "Keyboard Sounds: ON" : "Keyboard Sounds: OFF";
    public string ToggleLabel => Enabled ? "DISABLE SOUNDS" : "ENABLE SOUNDS";
    public bool Enabled
    {
        get => _settings.Enabled;
        set
        {
            _settings.Enabled = value; _hook.Enabled = value;
            if (!value) _audio.Stop();
            Notify(); Notify(nameof(Status)); Notify(nameof(MasterLabel)); Notify(nameof(ToggleLabel)); Changed();
        }
    }
    public SoundItem? SelectedSound
    {
        get => _selected;
        set { if (!Set(ref _selected, value)) return; _settings.SelectedSound = value?.Id ?? ""; Changed(); if (value != null) _audio.Warm(value.Path); }
    }
    public int Volume { get => _settings.Volume; set { _settings.Volume = Math.Clamp(value, 0, 100); _audio.Volume = _settings.Volume / 100f; Notify(); Changed(); } }
    public bool RandomMode { get => _settings.RandomMode; set { _settings.RandomMode = value; Notify(); Changed(); } }
    public bool ChaosMode { get => _settings.ChaosMode; set { _settings.ChaosMode = value; Notify(); Changed(); } }
    public int PlaybackIndex { get => (int)_settings.PlaybackMode; set { _settings.PlaybackMode = (PlaybackMode)Math.Clamp(value, 0, 2); Notify(); Notify(nameof(RepeatNotice)); Changed(); } }
    public int RepeatIndex { get => _settings.Repeat ? 1 : 0; set { _settings.Repeat = value == 1; _hook.Repeat = _settings.Repeat; Notify(); Notify(nameof(RepeatNotice)); Changed(); } }
    public bool IncludeModifiers { get => _settings.IncludeModifiers; set { _settings.IncludeModifiers = value; _hook.IncludeModifiers = value; Notify(); Changed(); } }
    public int MinimumInterval { get => _settings.MinimumInterval; set { _settings.MinimumInterval = Math.Clamp(value, 0, 500); Notify(); Changed(); } }
    public bool StartMinimized { get => _settings.StartMinimized; set { _settings.StartMinimized = value; Notify(); Changed(); } }
    public bool MinimizeToTray { get => _settings.MinimizeToTray; set { _settings.MinimizeToTray = value; Notify(); Changed(); } }
    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            try { StartupService.SetEnabled(value); _settings.StartWithWindows = value; Changed(); }
            catch (Exception e) { Message = "Windows startup could not be changed: " + e.Message; }
            Notify();
        }
    }
    public MainViewModel(string? dataRoot = null)
    {
        _store = new(dataRoot); _settings = _store.Load(); _library = new(_store.Root);
        _hook = new();
        ToggleCommand = new(_ => Enabled = !Enabled);
        PreviewCommand = new(p => Preview(p as SoundItem ?? SelectedSound), _ => !Busy);
        ImportCommand = new(async _ => await ImportAsync(), _ => !Busy);
        RemoveCommand = new(p => Remove(p as SoundItem ?? SelectedSound), p => !Busy && (p as SoundItem ?? SelectedSound)?.IsCustom == true);
        ExitCommand = new(_ => Application.Current.Shutdown());
        DismissCommand = new(_ => Message = "");
        StopCommand = new(_ => _audio.Stop());
        SinglePressCommand = new(_ => { RepeatIndex = 0; MinimumInterval = 50; Message = "Once per physical press enabled, with a 50 ms minimum interval."; });
        AudioDiagnosticsCommand = new(_ =>
        {
            _diagnosing = true; _audio.Stop();
            try { new Views.AudioDiagnosticsWindow(_audio, _hook, () => SelectedSound, pause => _diagnosing = pause) { Owner = Application.Current.MainWindow }.ShowDialog(); }
            finally { _diagnosing = false; }
        });
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Save(); };
        _pulseTimer.Tick += (_, _) => { if (Interlocked.Exchange(ref _pulse, 0) != 0) Activity = "Sound triggered · " + DateTime.Now.ToString("HH:mm:ss"); else Activity = Enabled ? "Listening locally · ready for a key" : "Keyboard sounds are off"; };
        foreach (var sound in _library.Load(_settings)) Add(sound);
        _selected = Sounds.FirstOrDefault(s => s.Id == _settings.SelectedSound) ?? Sounds.FirstOrDefault();
        _settings.SelectedSound = _selected?.Id ?? "";
        try { _settings.StartWithWindows = StartupService.IsEnabled(); } catch { _settings.StartWithWindows = false; }
        _audio.Volume = Volume / 100f;
        _audio.Error += AudioError; _audio.Played += () => Interlocked.Exchange(ref _pulse, 1);
        _hook.Enabled = Enabled; _hook.Repeat = _settings.Repeat; _hook.IncludeModifiers = IncludeModifiers;
        _hook.Pressed += OnPress;
        _hook.ToggleRequested += () => _dispatcher.BeginInvoke(() => { Enabled = !Enabled; HotkeyToggled?.Invoke(); });
        PublishRouting();
        foreach (var sound in Sounds) _audio.Warm(sound.Path);
        if (_store.Warning != null) Message = _store.Warning;
        else if (!HotkeyAvailable) Message = "Ctrl + Shift + F12 is already used by another app. The master switch and tray menu still work.";
        else if (Sounds.Count == 0) Message = "No sounds found. Add a custom WAV or MP3 to get started.";
        _pulseTimer.Start();
    }
    public event Action? HotkeyToggled;
    private void AudioError(string message) => _dispatcher.BeginInvoke(() => Message = message);
    private void OnPress()
    {
        if (_diagnosing) return;
        var route = Volatile.Read(ref _routing);
        long now = Environment.TickCount64;
        if (now - _lastPress < route.Interval) return;
        string? path = route.RandomMode || route.Chaos ? (route.Random.Length == 0 ? null : route.Random[Random.Shared.Next(route.Random.Length)]) : route.Selected;
        if (path == null) return;
        _lastPress = now;
        _audio.Play(path, route.Playback, route.Chaos ? .65f + Random.Shared.NextSingle() * .35f : 1, _hook.LastPressWasRepeat ? "keyboard/repeat" : "keyboard/physical");
    }
    private void PublishRouting() => Volatile.Write(ref _routing, new Routing(_selected?.Path, Sounds.Where(s => s.RandomEnabled).Select(s => s.Path).ToArray(), RandomMode, ChaosMode, _settings.PlaybackMode, MinimumInterval));
    private void Changed() { PublishRouting(); _saveTimer.Stop(); _saveTimer.Start(); }
    private void Add(SoundItem item) { item.PropertyChanged += SoundChanged; Sounds.Add(item); Notify(nameof(LibraryCount)); }
    private void SoundChanged(object? sender, PropertyChangedEventArgs args) => Changed();
    private void Preview(SoundItem? item) { if (item != null) _audio.Play(item.Path, PlaybackMode.RestartSound); }
    private async Task ImportAsync()
    {
        var picker = new OpenFileDialog { Title = "Add your sound effect", Filter = "Audio files|*.wav;*.mp3;*.m4a;*.aac;*.flac|WAV|*.wav|MP3|*.mp3", Multiselect = true };
        if (picker.ShowDialog() != true) return;
        Busy = true;
        try
        {
            foreach (var file in picker.FileNames)
            {
                try { var item = await _library.ImportAsync(file); Add(item); SelectedSound = item; Message = $"Added {item.Name}. Ready to preview."; Save(); }
                catch (Exception e) { Message = $"Could not import {Path.GetFileName(file)}: {e.Message}"; break; }
            }
        }
        finally { Busy = false; }
    }
    private void Remove(SoundItem? item)
    {
        if (item?.IsCustom != true) return;
        if (MessageBox.Show($"Remove ‘{item.Name}’ from FunnyKeys? Your original file will be kept.", "Remove custom sound", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            _audio.Stop(); _library.Remove(item); item.PropertyChanged -= SoundChanged; Sounds.Remove(item);
            if (SelectedSound == item) SelectedSound = Sounds.FirstOrDefault();
            Notify(nameof(LibraryCount)); Changed(); Save(); Message = "Custom sound removed. The original file was kept.";
        }
        catch (Exception e) { Message = "Could not remove sound: " + e.Message; }
    }
    public void Save()
    {
        _settings.CustomSounds = Sounds.Where(s => s.IsCustom).Select(s => new CustomSound(Path.GetFileName(s.Path), s.Name)).ToList();
        _settings.RandomExcluded = Sounds.Where(s => !s.RandomEnabled).Select(s => s.Id).ToList();
        try { _store.Save(_settings); } catch (Exception e) { Message = "Settings could not be saved: " + e.Message; }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _saveTimer.Stop(); _pulseTimer.Stop(); _hook.Dispose(); _audio.Error -= AudioError; _audio.Dispose(); Save();
    }
}
