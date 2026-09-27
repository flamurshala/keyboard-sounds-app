using FunnyKeys.Services;
using FunnyKeys.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

static class AudioStages
{
    public static int Run(string path)
    {
        foreach(string stage in new[]{"B direct AudioFileReader", "C cached single voice", "D NAudio mixer single voice", "E two overlapping voices"})
        {
            using var reader=new AudioFileReader(path);
            ISampleProvider provider=reader;
            double seconds=reader.TotalTime.TotalSeconds;
            if(stage.StartsWith("C")) provider=new VolumeSampleProvider(new CachedSoundSampleProvider(new CachedSound(path))) {Volume=.7f};
            if(stage.StartsWith("D") || stage.StartsWith("E")) {
                var mixer=new VoiceMixer {Volume=1};var sound=new CachedSound(path);
                mixer.Add(sound,PlaybackMode.AllowOverlap);
                if(stage.StartsWith("E"))mixer.Add(sound,PlaybackMode.AllowOverlap);
                provider=mixer;
            }
            using var stopped=new ManualResetEventSlim();Exception? failure=null;
            using var output=new WaveOutEvent {DesiredLatency=80,NumberOfBuffers=3};
            output.PlaybackStopped+=(_,e)=>{failure=e.Exception;stopped.Set();};
            output.Init(provider);output.Play();
            if(stage.StartsWith("B") || stage.StartsWith("C")) {
                if(!stopped.Wait(TimeSpan.FromSeconds(seconds+5)))throw new Exception("Playback timed out");
            } else {Thread.Sleep(TimeSpan.FromSeconds(seconds+.5));output.Stop();stopped.Wait(5000);}
            if(failure!=null)throw failure;
            Console.WriteLine(stage+": Windows output completed without error; audible quality requires listening.");
        }
        return 0;
    }
}
