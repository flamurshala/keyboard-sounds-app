using FunnyKeys.Models;
using FunnyKeys.Services;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;

static class AudioQualityChecks
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Register(List<(string Name, Action Run)> tests, string root, string[] files)
    {
        void Test(string name, Action run) => tests.Add(("Audio quality: " + name, run));
        Test("immutable cache and independent playback positions", () => {
            float[] data = [.1f,.2f,.3f,.4f]; var sound=new CachedSound(data); data[0]=1;
            var a=new CachedSoundSampleProvider(sound); var b=new CachedSoundSampleProvider(sound);
            var output=new float[6]; Check(a.Read(output,1,2)==2 && output[1]==.1f && output[2]==.2f,"Cache mutated");
            Check(b.Read(output,0,4)==4 && output[0]==.1f && output[3]==.4f,"Shared cursor");
            Check(a.Read(output,0,4)==2 && output[0]==.3f && a.Read(output,0,2)==0,"Bad end/cursor");
        });
        Test("cached provider supports NAudio byte-backed WaveBuffer output", () => {
            var cached=new CachedSound(new float[]{.1f,.2f,.3f,.4f});
            var provider=new SampleToWaveProvider(new CachedSoundSampleProvider(cached));
            var bytes=new byte[16];Check(provider.Read(bytes,0,16)==16,"Short byte-backed read");
            Check(BitConverter.ToSingle(bytes,0)==.1f && BitConverter.ToSingle(bytes,12)==.4f,"Wrong float bytes");
        });
        Test("restart replaces immediately; stop is immediate silence", () => {
            var m=new VoiceMixer {Volume=1}; var b=new float[100];
            m.Add(Enumerable.Repeat(.8f,4000).ToArray(),PlaybackMode.RestartSound);m.Read(b,0,100);
            m.Add(Enumerable.Repeat(-.8f,4000).ToArray(),PlaybackMode.RestartSound);m.Read(b,0,100);
            Check(b.All(x=>Math.Abs(x+.56f)<1e-6),"Old samples or transition survived restart");
            m.Clear();m.Read(b,0,100);Check(b.All(x=>x==0),"Stop retained audio");
        });
        Test("six-voice limit and bounded summing without limiter", () => {
            var m=new VoiceMixer {Volume=1};var s=new CachedSound(Enumerable.Repeat(1f,4000).ToArray());
            for(int i=0;i<6;i++)Check(m.Add(s,PlaybackMode.AllowOverlap),"Voice refused");
            Check(!m.Add(s,PlaybackMode.AllowOverlap),"Seventh voice admitted");
            var b=new float[100];m.Read(b,0,100);
            Check(b.All(x=>Math.Abs(x-.7f)<1e-6) && m.ClippedSamples==0,"Overlap clipped");
        });
        Test("44.1/48 kHz mono/stereo PCM16/24, pitch, interleaving and master gain", () => {
            foreach(int rate in new[]{44100,48000}) foreach(int channels in new[]{1,2}) foreach(int bits in new[]{16,24}) {
                string path=Path.Combine(root,$"matrix-{rate}-{channels}-{bits}.wav");
                using(var w=new WaveFileWriter(path,new WaveFormat(rate,bits,channels)))
                    for(int frame=0;frame<rate/2;frame++) for(int ch=0;ch<channels;ch++)
                        w.WriteSample((float)(.4*Math.Sin(2*Math.PI*(ch==0?440:880)*frame/rate)));
                var data=AudioPlaybackService.Decode(path);Check(Math.Abs(data.Length-48000)<=4,"Wrong resampled duration");
                for(int ch=0;ch<2;ch++) {
                    int crosses=0;for(int i=100+ch;i<data.Length-2;i+=2)if(data[i]<=0 && data[i+2]>0)crosses++;
                    double hz=crosses/((data.Length-100)/96000.0);
                    Check(Math.Abs(hz-(ch==0||channels==1?440:880))<5,"Pitch or interleaving changed");
                }
                VerifySignal(data);
            }
        });
        Test("ten simulated physical keys yield ten requests; repeats suppressed", () => {
            var policy=new KeyPressPolicy();int requests=0;
            for(int i=0;i<10;i++) {
                if(policy.Process(65,true,false,false))requests++;
                for(int r=0;r<20;r++)if(policy.Process(65,true,false,false))requests++;
                policy.Process(65,false,false,false);
            }
            Check(requests==10,"Duplicate triggers"); Console.WriteLine("  Simulated ten down/up pairs plus 200 repeat downs: requests="+requests);
        });
        if(files.Length>0) Test("supplied files: standard decoder/resampler versus cache and mixer", () => {
            foreach(string file in files) {
                using var reader=new AudioFileReader(file);ISampleProvider reference=reader;
                if(reference.WaveFormat.Channels==1)reference=new MonoToStereoSampleProvider(reference);
                if(reference.WaveFormat.SampleRate!=48000)reference=new WdlResamplingSampleProvider(reference,48000);
                var expected=new List<float>();var buffer=new float[8192];int n;
                while((n=reference.Read(buffer,0,buffer.Length))>0)expected.AddRange(buffer.Take(n));
                var decoded=AudioPlaybackService.Decode(file);
                Check(expected.SequenceEqual(decoded),"Cache differs from standard conversion");VerifySignal(decoded);
                Console.WriteLine("  "+Path.GetFileName(file)+": "+new CachedSound(decoded).Statistics+" | Cache/mixer max error: 0");
            }
        });
    }
    static void VerifySignal(float[] data)
    {
        foreach(float volume in new[]{.25f,.75f,1f}) {
            var m=new VoiceMixer {Volume=volume};m.Add(new CachedSound(data),PlaybackMode.AllowOverlap);
            var rendered=new float[data.Length+200];m.Read(rendered,4,data.Length+100);
            for(int i=0;i<data.Length;i++)Check(rendered[i+4]==Math.Clamp(data[i]*.7f*volume,-1,1),"Signal changed or master applied twice");
            Check(rendered.Skip(data.Length+4).All(x=>x==0),"End not silent");
            Check(m.ClippedSamples==0,"Single voice reached final clamp");
        }
    }
}

