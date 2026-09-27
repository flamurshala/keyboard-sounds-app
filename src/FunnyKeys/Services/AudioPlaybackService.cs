using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FunnyKeys.Models;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace FunnyKeys.Services;
public sealed class AudioPlaybackService : IDisposable, IMMNotificationClient
{
    private sealed record Request(string Path, PlaybackMode Mode, float Gain, int Generation, bool WarmOnly, long Id, long QueuedAt, string Source);
    private sealed record Cached(CachedSound Samples, long Stamp, long Length);
    private readonly Channel<Request> _requests = Channel.CreateBounded<Request>(new BoundedChannelOptions(16) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly Dictionary<string, Cached> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly VoiceMixer _mixer = new();
    private readonly Task _worker;
    private WaveOutEvent? _output;
    private MMDeviceEnumerator? _devices;
    private int _generation, _reset;
    private long _lastError;
    private bool _disposed;
    private readonly ConcurrentQueue<string> _trace = new();
    private long _triggerId, _accepted, _ignored;
    private int _outputInitializations;
    private string _deviceId = "", _deviceDescription = "Default Windows output (initializing)";
    public volatile bool DiagnosticsEnabled;
    public long TotalPlayRequests => Interlocked.Read(ref _triggerId);
    public int OutputInitializations => Volatile.Read(ref _outputInitializations);
    public string Diagnostics => $"WaveOut shared Windows mapper | 80 ms output / 3 buffers / no DSP\nMixer: 48000 Hz / 2 channels / float32\nOutput: {_deviceDescription}\nActive voices: {_mixer.VoiceCount}/{VoiceMixer.MaxVoices} | Final clamp samples: {_mixer.ClippedSamples}\nAudio output initialization count: {OutputInitializations} | TotalPlayRequests: {TotalPlayRequests} | Accepted: {Interlocked.Read(ref _accepted)} | Rejected/stale: {Interlocked.Read(ref _ignored)}\n\n" + string.Join("\n", _trace.Reverse());
    public event Action<string>? Error;
    public event Action? Played;
    public float Volume { set => _mixer.Volume = value; }
    public AudioPlaybackService()
    {
        try { _devices = new(); _devices.RegisterEndpointNotificationCallback(this); } catch { _devices?.Dispose(); _devices = null; }
        _worker = Task.Run(Work);
    }
    public void Play(string path, PlaybackMode mode, float gain = 1, string source = "preview") => _requests.Writer.TryWrite(new(path, mode, gain, Volatile.Read(ref _generation), false, Interlocked.Increment(ref _triggerId), Environment.TickCount64, source));
    public void Warm(string path) => _requests.Writer.TryWrite(new(path, PlaybackMode.RestartSound, 1, Volatile.Read(ref _generation), true, 0, Environment.TickCount64, "cache"));
    public void ClearDiagnostics() { while (_trace.TryDequeue(out _)) { } }
    private void Trace(Request request, string result)
    {
        if (!DiagnosticsEnabled) return;
        _trace.Enqueue($"{DateTime.Now:HH:mm:ss.fff} #{request.Id} {request.Source} | {Path.GetFileName(request.Path)} | {request.Mode} | {result} | voices={_mixer.VoiceCount}");
        while (_trace.Count > 80) _trace.TryDequeue(out _);
    }
    public void Stop() { Interlocked.Increment(ref _generation); _mixer.Clear(); }
    public static float[] Decode(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException("The sound file is missing. Re-import it or select another sound.");
        if (file.Length > 20 * 1024 * 1024) throw new InvalidDataException("Choose an audio file smaller than 20 MB.");
        using var reader = new AudioFileReader(path);
        if (reader.TotalTime.TotalSeconds > 15) throw new InvalidDataException("Choose a short sound effect (15 seconds or less).");
        ISampleProvider provider = reader;
        if (provider.WaveFormat.Channels == 1) provider = new MonoToStereoSampleProvider(provider);
        if (provider.WaveFormat.Channels != 2) throw new InvalidDataException("Only mono or stereo audio is supported.");
        if (provider.WaveFormat.SampleRate != 48000) provider = new WdlResamplingSampleProvider(provider, 48000);
        var samples = new List<float>(); var buffer = new float[8192]; int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (samples.Count + read > 48000 * 2 * 15) throw new InvalidDataException("The decoded sound exceeds 15 seconds.");
            for (int i = 0; i < read; i++)
            {
                if (!float.IsFinite(buffer[i])) throw new InvalidDataException("This sound contains invalid floating-point audio.");
                samples.Add(buffer[i]); // Preserve decoder output. Overload control belongs after mixing/master gain.
            }
        }
        if (samples.Count == 0) throw new InvalidDataException("This file contains no playable audio.");
        return samples.ToArray();
    }
    private CachedSound GetSamples(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) { _cache.Remove(path); throw new FileNotFoundException("A sound file is missing. Select another sound or re-import it."); }
        if (_cache.TryGetValue(path, out var cached) && cached.Stamp == info.LastWriteTimeUtc.Ticks && cached.Length == info.Length) return cached.Samples;
        var data = new CachedSound(path);
        // A hard cache limit prevents a large custom library from consuming unbounded RAM.
        if (_cache.Values.Sum(c => (long)c.Samples.SampleCount * 4) + data.SampleCount * 4L > 128 * 1024 * 1024) _cache.Clear();
        _cache[path] = new(data, info.LastWriteTimeUtc.Ticks, info.Length);
        return data;
    }
    private async Task Work()
    {
        try { EnsureOutput(); } catch (Exception e) { DisposeOutput(); Error?.Invoke(e.Message); }
        await foreach (var request in _requests.Reader.ReadAllAsync())
        {
            if (request.Generation != Volatile.Read(ref _generation)) continue;
            if (!request.WarmOnly && request.Source.StartsWith("keyboard/", StringComparison.Ordinal) && Environment.TickCount64 - request.QueuedAt > 250)
            { Interlocked.Increment(ref _ignored); Trace(request, "stale queue entry dropped"); continue; }
            try
            {
                var samples = GetSamples(request.Path);
                if (request.WarmOnly) continue;
                if (Interlocked.Exchange(ref _reset, 0) != 0) DisposeOutput();
                EnsureOutput();
                if (request.Generation != Volatile.Read(ref _generation)) continue;
                bool accepted = _mixer.Add(samples, request.Mode, request.Gain);
                // Close the race with Stop() after a queued request has been decoded.
                if (request.Generation != Volatile.Read(ref _generation)) _mixer.Clear();
                else if (accepted) { Interlocked.Increment(ref _accepted); Trace(request, "accepted"); Played?.Invoke(); }
                else { Interlocked.Increment(ref _ignored); Trace(request, "ignored (busy/voice cap)"); }
            }
            catch (Exception e)
            {
                // Bad files must not tear down the persistent device. Device notifications handle resets.
                if (!request.WarmOnly && Environment.TickCount64 - _lastError > 3000)
                { _lastError = Environment.TickCount64; Error?.Invoke($"Cannot play this sound: {e.Message}"); }
            }
        }
        DisposeOutput(); _cache.Clear();
    }
    private void EnsureOutput()
    {
        if (_output != null) return;
        try
        {
            using var endpoint = _devices?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            if (endpoint != null) { _deviceId = endpoint.ID; _deviceDescription = $"{endpoint.FriendlyName} | Windows mix: {endpoint.AudioClient.MixFormat}"; }
        }
        catch { _deviceDescription = "Windows default output"; }
        var output = new WaveOutEvent { DesiredLatency = 80, NumberOfBuffers = 3, DeviceNumber = -1 };
        try
        {
            output.PlaybackStopped += OnPlaybackStopped;
            output.Init(_mixer); output.Play();
            _output = output;
            Interlocked.Increment(ref _outputInitializations);
        }
        catch { output.PlaybackStopped -= OnPlaybackStopped; output.Dispose(); throw; }
    }
    private void OnPlaybackStopped(object? sender, StoppedEventArgs args)
    {
        if (args.Exception != null) { Interlocked.Exchange(ref _reset, 1); Error?.Invoke("Audio device unavailable. Reconnect an output device; the next press will retry."); }
    }
    private void DisposeOutput()
    {
        var output = _output; _output = null;
        if (output == null) return;
        output.PlaybackStopped -= OnPlaybackStopped;
        try { output.Stop(); } catch { }
        try { output.Dispose(); } catch { }
        _mixer.Clear();
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _devices?.UnregisterEndpointNotificationCallback(this); _devices?.Dispose();
        Stop(); _requests.Writer.TryComplete(); _worker.GetAwaiter().GetResult();
    }
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string id) { if (flow == DataFlow.Render && role == Role.Console && id != _deviceId) Interlocked.Exchange(ref _reset, 1); }
    public void OnDeviceStateChanged(string id, DeviceState state) { if (id == _deviceId && state != DeviceState.Active) Interlocked.Exchange(ref _reset, 1); }
    public void OnDeviceAdded(string id) { }
    public void OnDeviceRemoved(string id) { if (id == _deviceId) Interlocked.Exchange(ref _reset, 1); }
    public void OnPropertyValueChanged(string id, PropertyKey key) { }
}



