using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FunnyKeys.Models;
using FunnyKeys.Services;
using NAudio.Wave;

namespace FunnyKeys.Views;

public sealed class AudioDiagnosticsWindow : Window
{
    private readonly AudioPlaybackService _audio;
    private readonly Func<SoundItem?> _selected;
    private readonly TextBox _log;
    private readonly TextBlock _format;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _tools = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0,8,0,12) };
    private string? _testFile;
    private bool _closed;
    public AudioDiagnosticsWindow(AudioPlaybackService audio, GlobalKeyboardHookService hook, Func<SoundItem?> selected, Action<bool>? pauseKeyboard = null)
    {
        _audio = audio; _selected = selected;
        Title = "FunnyKeys - Audio diagnostics"; Width = 850; Height = 650;
        MinWidth = 680; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(16,18,23)); Foreground = Brushes.White;
        var panel = new DockPanel { Margin = new Thickness(24) }; Content = panel;
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); panel.Children.Add(header);
        header.Children.Add(new TextBlock { Text = "Audio diagnostics", FontSize = 24, FontWeight = FontWeights.Bold });
        header.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text = "Start with PLAY CLEAN TEST: one full sound, keyboard paused, no restart, random mode or DSP. Gain is 0.70 times master volume. Then test overlap and real keyboard triggers. Counters retain no typed text.", Margin = new Thickness(0,10,0,12) });
        _format = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) }; header.Children.Add(_format);
        var pause = new CheckBox { Content = "Pause keyboard sounds for preview tests", IsChecked = true };
        pause.Checked += (_,_) => pauseKeyboard?.Invoke(true);
        pause.Unchecked += (_,_) => pauseKeyboard?.Invoke(false);
        header.Children.Add(pause);
        var record = new CheckBox { Content = "Record temporary trigger diagnostics (memory only)" };
        record.Checked += (_,_) => _audio.DiagnosticsEnabled = true;
        record.Unchecked += (_,_) => { _audio.DiagnosticsEnabled = false; _audio.ClearDiagnostics(); };
        header.Children.Add(record); header.Children.Add(_buttons);
                Button("PLAY CLEAN TEST", async () => {
            pause.IsChecked = true; pause.IsEnabled = false;
            try {
                var sound = _selected(); if (sound == null) return;
                _audio.Stop(); await Task.Delay(120);
                if (_closed) return;
                var cached = await Task.Run(() => new CachedSound(sound.Path));
                _format.Text = sound.Name + "\n" + cached.Statistics;
                _audio.Play(sound.Path, PlaybackMode.IgnoreUntilFinished, source: "clean single voice");
                await Task.Delay(TimeSpan.FromSeconds(cached.SampleCount / 96000.0 + .3));
            } finally { pause.IsEnabled = true; }
        });
        Button("Restart once", () => PlayOnce(PlaybackMode.RestartSound));
        Button("Add overlap voice", () => PlayOnce(PlaybackMode.AllowOverlap));
        Button("Play if idle", () => PlayOnce(PlaybackMode.IgnoreUntilFinished));
        var tools = _tools; header.Children.Add(tools);
        var wav = new Button { Content = "Play WAV test", Margin = new Thickness(0,0,8,0) };
        wav.Click += (_,_) => PlayWav(); tools.Children.Add(wav);
        var stop = new Button { Content = "Stop audio" }; stop.Click += (_,_) => _audio.Stop(); tools.Children.Add(stop);
        _log = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(25,28,35)), Foreground = Brushes.White, FontFamily = new FontFamily("Consolas"), FontSize = 12, Padding = new Thickness(12) };
        panel.Children.Add(_log);
        _timer.Tick += (_,_) => _log.Text = hook.Diagnostics + "\n" + _audio.Diagnostics;
        Loaded += async (_,_) =>
        {
            try
            {
                var sound = _selected();
                string description = sound == null ? "No selected sound." : await Task.Run(() =>
                { using var reader = new AudioFileReader(sound.Path); return $"Selected: {sound.Name} | {reader.WaveFormat.SampleRate} Hz / {reader.WaveFormat.Channels} ch / {reader.TotalTime.TotalSeconds:F3} s\n{new CachedSound(sound.Path).Statistics}"; });
                if (!_closed) _format.Text = description;
            }
            catch (Exception e) { _format.Text = e.Message; }
        };
        Closed += (_,_) => { _closed = true; _timer.Stop(); _audio.Stop(); _audio.DiagnosticsEnabled = false; _audio.ClearDiagnostics(); pauseKeyboard?.Invoke(false); };
        _timer.Start();
    }
    private void Button(string label, Func<Task> action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0,8,8,0), Padding = new Thickness(10,8,10,8) };
        button.Click += async (_,_) =>
        {
            _buttons.IsEnabled = false; _tools.IsEnabled = false;
            try { await action(); } catch (Exception e) { _format.Text = e.Message; }
            finally { if (!_closed) { _buttons.IsEnabled = true; _tools.IsEnabled = true; } }
        };
        _buttons.Children.Add(button);
    }
    private Task PlayOnce(PlaybackMode mode)
    {
        var sound = _selected();
        if (sound != null) _audio.Play(sound.Path, mode, source: "diagnostic");
        return Task.CompletedTask;
    }
    private void PlayWav()
    {
        try
        {
            // Reproducible PCM16 / 44100 Hz / stereo reference; no imported source is changed.
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FunnyKeys", "Diagnostics");
            Directory.CreateDirectory(directory); _testFile = Path.Combine(directory, "reference-440Hz.wav");
            if (!File.Exists(_testFile))
                using (var writer = new WaveFileWriter(_testFile, new WaveFormat(44100, 16, 2)))
                    for (int i = 0; i < 44100; i++)
                    {
                        float sample = (float)(.4 * Math.Sin(2 * Math.PI * 440 * i / 44100) * Math.Min(1,i/220.0) * Math.Min(1,(44100-i)/220.0));
                        writer.WriteSample(sample); writer.WriteSample(sample);
                    }
            _audio.Play(_testFile, PlaybackMode.RestartSound, source: "reference WAV");
            _format.Text = "Reference: " + _testFile + "\nOpen this same file in Windows Media Player for a listening comparison.";
        }
        catch (Exception e) { _format.Text = e.Message; }
    }
}


