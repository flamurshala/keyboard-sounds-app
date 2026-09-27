using System;
using NAudio.Wave;
namespace FunnyKeys.Services;
public sealed class CachedSound
{
    private readonly float[] _data;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
    public int SampleCount => _data.Length;
    public float Peak { get; }
    public double Rms { get; }
    public string Statistics => $"48000 Hz stereo float32 | Samples: {SampleCount} | Peak: {Peak:F6} | RMS: {Rms:F6} | NaN: 0 | Infinity: 0";
    public CachedSound(string path) : this(AudioPlaybackService.Decode(path)) { }
    public CachedSound(float[] samples)
    {
        if (samples.Length == 0 || samples.Length % 2 != 0) throw new ArgumentException("Audio must contain complete stereo frames.");
        _data = (float[])samples.Clone();
        double energy = 0;
        foreach (float sample in _data)
        {
            if (!float.IsFinite(sample)) throw new ArgumentException("Audio contains NaN or infinity.");
            Peak = Math.Max(Peak, Math.Abs(sample)); energy += (double)sample * sample;
        }
        Rms = Math.Sqrt(energy / _data.Length);
    }
    internal int CopyTo(int position, float[] buffer, int offset, int count)
    {
        int read = Math.Min(_data.Length - position, count);
        // NAudio may expose a byte-backed WaveBuffer as float[]. BlockCopy supports that alias.
        Buffer.BlockCopy(_data, position * sizeof(float), buffer, offset * sizeof(float), read * sizeof(float));
        return read;
    }
}
public sealed class CachedSoundSampleProvider(CachedSound sound) : ISampleProvider
{
    private int _position;
    public WaveFormat WaveFormat => sound.WaveFormat;
    public int Read(float[] buffer, int offset, int count)
    {
        int read = sound.CopyTo(_position, buffer, offset, count);
        _position += read;
        return read;
    }
}

