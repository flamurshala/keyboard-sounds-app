using System;
using System.Linq;
using FunnyKeys.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
namespace FunnyKeys.Services;
// NAudio owns summing, cursors belong to providers; this class only controls admission/gain.
public sealed class VoiceMixer : ISampleProvider
{
    public const int MaxVoices = 6;
    public const float DefaultVoiceGain = .70f;
    private readonly object _gate = new();
    private readonly MixingSampleProvider _mixer = new(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)) { ReadFully = true };
    public WaveFormat WaveFormat => _mixer.WaveFormat;
    public volatile float Volume = .75f;
    public long ClippedSamples { get; private set; }
    public int VoiceCount { get { lock (_gate) return _mixer.MixerInputs.Count(); } }
    public bool Add(float[] data, PlaybackMode mode, float gain = 1) => Add(new CachedSound(data), mode, gain);
    public bool Add(CachedSound sound, PlaybackMode mode, float gain = 1)
    {
        lock (_gate)
        {
            int count = _mixer.MixerInputs.Count();
            if (mode == PlaybackMode.IgnoreUntilFinished && count != 0) return false;
            if (mode == PlaybackMode.AllowOverlap && count >= MaxVoices) return false;
            if (mode == PlaybackMode.RestartSound) _mixer.RemoveAllMixerInputs();
            _mixer.AddMixerInput(new VolumeSampleProvider(new CachedSoundSampleProvider(sound))
                { Volume = DefaultVoiceGain * Math.Clamp(gain, 0, 1) });
            return true;
        }
    }
    public void Clear() { lock (_gate) _mixer.RemoveAllMixerInputs(); }
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            // Arithmetic mean provides bounded overlap headroom, without an envelope or signal analysis.
            int voices = Math.Max(1, _mixer.MixerInputs.Count());
            int read = _mixer.Read(buffer, offset, count);
            float gain = Math.Clamp(Volume, 0, 1) / voices;
            for (int i = offset; i < offset + read; i++)
            {
                float sample = buffer[i] * gain;
                if (Math.Abs(sample) > 1) ClippedSamples++;
                buffer[i] = Math.Clamp(sample, -1, 1);
            }
            return read;
        }
    }
}
