using FunnyKeys.Models;
using FunnyKeys.Services;
using NAudio.Wave;
using System.IO;

if (args.Length == 2 && args[0] == "--stages") return AudioStages.Run(args[1]);

if (args.Contains("--listen"))
{
    using var hook = new GlobalKeyboardHookService { Enabled = true };
    using var audio = new AudioPlaybackService();
    string sample = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "Keyboard click.wav");
    int presses = 0, toggles = 0, played = 0;
    audio.Volume = .2f;
    hook.Pressed += () => { Interlocked.Increment(ref presses); audio.Play(sample, PlaybackMode.RestartSound); };
    hook.ToggleRequested += () => { Interlocked.Increment(ref toggles); hook.Enabled = !hook.Enabled; };
    audio.Played += () => Interlocked.Increment(ref played);
    Console.WriteLine("READY: 45-second global hook probe. Only aggregate counts, no keys or text are retained.");
    Thread.Sleep(45000);
    Console.WriteLine($"Global triggers: {presses}; audio submissions: {played}; hotkey toggles: {toggles}");
    return presses > 0 && played > 0 && toggles > 0 ? 0 : 1;
}

var tests = new List<(string Name, Action Run)>();
void Test(string name, Action run) => tests.Add((name, run));
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
string root = Path.Combine(Path.GetTempPath(), "FunnyKeys-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string wav = Path.Combine(root, "valid.wav");
using (var writer = new WaveFileWriter(wav, new WaveFormat(22050, 16, 1)))
    for (int i = 0; i < 2205; i++) writer.WriteSample((float)Math.Sin(i * .1) * .25f);
Test("Physical press, repeat and release policy", () => {
    var p = new KeyPressPolicy();
    Check(p.Process(65, true, false, false)); Check(!p.Process(65, true, false, false));
    Check(p.Process(66, true, false, false)); Check(!p.Process(65, false, false, false));
    Check(p.Process(65, true, false, false)); Check(p.Process(65, true, true, false));
    p.Clear(); Check(p.Process(65, true, false, false));
});
Test("Modifiers and special keys", () => {
    var p = new KeyPressPolicy();
    foreach (int key in new[] {16,17,18,91,92,160,161,162,163,164,165}) { Check(!p.Process(key,true,false,false)); p.Clear(); Check(p.Process(key,true,false,true)); }
    foreach (int key in new[] {32,13,8,37,38,39,40,112,123,48}) Check(p.Process(key,true,false,false));
});
Test("Playback modes, bounded overlap and volume cap", () => {
    var m = new VoiceMixer { Volume = .5f }; var b = new float[1200];
    Check(m.Add(Enumerable.Repeat(.8f,4000).ToArray(),PlaybackMode.RestartSound));
    Check(!m.Add(Enumerable.Repeat(-.8f,4000).ToArray(),PlaybackMode.IgnoreUntilFinished));
    m.Read(b,0,b.Length);Check(Math.Abs(b[1000]-.28f)<.00001 && m.VoiceCount==1);
    m.Add(Enumerable.Repeat(-.8f,4000).ToArray(),PlaybackMode.RestartSound);m.Read(b,0,b.Length);
    Check(Math.Abs(b[1100]+.28f)<.00001 && m.VoiceCount==1);
    for(int i=0;i<100;i++)m.Add(Enumerable.Repeat(.8f,4000).ToArray(),PlaybackMode.AllowOverlap);
    Check(m.VoiceCount==VoiceMixer.MaxVoices);m.Read(b,0,b.Length);Check(b.All(x=>Math.Abs(x)<=1.000001));
    m.Clear();m.Read(b,0,b.Length);m.Read(b,0,b.Length);Check(b.All(x=>x==0));
});
Test("Settings roundtrip, clamping and corrupt recovery", () => {
    var s = new SettingsService(root); Check(!s.Load().Enabled);
    var a = new AppSettings { Enabled=true, Volume=27, RandomMode=true, Repeat=true, MinimumInterval=0, PlaybackMode=PlaybackMode.AllowOverlap, CustomSounds = new() {new("a.wav","Custom")} };
    s.Save(a); var b=s.Load(); Check(b.Enabled && b.Volume==27 && b.Repeat && b.MinimumInterval==0 && b.CustomSounds.Count==1);
    File.WriteAllText(Path.Combine(root,"settings.json"),"{\"Volume\":200,\"MinimumInterval\":-5,\"PlaybackMode\":42,\"CustomSounds\":null}");
    b=s.Load(); Check(b.Volume==100 && b.MinimumInterval==0 && b.CustomSounds.Count==0 && b.PlaybackMode==PlaybackMode.RestartSound);
    File.WriteAllText(Path.Combine(root,"settings.json"),"not json"); Check(!s.Load().Enabled && s.Warning != null);
});
Test("WAV decode resamples to stereo and validates malformed input", () => {
    var samples=AudioPlaybackService.Decode(wav); Check(samples.Length>8000 && samples.All(float.IsFinite));
    var bad=Path.Combine(root,"bad.wav"); File.WriteAllText(bad,"not audio");
    bool failed=false; try { AudioPlaybackService.Decode(bad); } catch {failed=true;} Check(failed);
    failed=false; try { AudioPlaybackService.Decode(Path.Combine(root,"missing.wav")); } catch(FileNotFoundException) {failed=true;} Check(failed);
});
Test("Import makes validated private copy; removal preserves original", () => {
    var l=new SoundLibraryService(root); var sound=l.ImportAsync(wav).GetAwaiter().GetResult();
    Check(sound.IsCustom && File.Exists(sound.Path) && sound.Path!=wav);
    l.Remove(sound); Check(!File.Exists(sound.Path) && File.Exists(wav));
    bool failed=false; try { l.Remove(new SoundItem{Id="builtin:x",Name="x",Path=wav}); } catch(InvalidOperationException){failed=true;} Check(failed && File.Exists(wav));
    var s=new AppSettings {CustomSounds=new(){new("..\\outside.wav","bad"),new("C:\\bad.wav","bad")}};
    Check(l.Load(s).All(x=>!x.IsCustom));
});
Test("Import rejects oversized and long audio", () => {
    var large=Path.Combine(root,"large.wav"); using(var f=File.Create(large)) f.SetLength(21*1024*1024);
    bool failed=false; try { new SoundLibraryService(root).ImportAsync(large).GetAwaiter().GetResult(); } catch(InvalidDataException){failed=true;} Check(failed);
    var longFile=Path.Combine(root,"long.wav"); using(var w=new WaveFileWriter(longFile,new WaveFormat(8000,16,1))) w.Write(new byte[8000*2*16],0,8000*2*16);
    failed=false; try { AudioPlaybackService.Decode(longFile); } catch(InvalidDataException){failed=true;} Check(failed);
});
Test("Global hook starts and releases hotkey on disposal", () => {
    bool available;
    using(var hook = new GlobalKeyboardHookService()) available=hook.HotkeyAvailable;
    using(var hook = new GlobalKeyboardHookService()) Check(hook.HotkeyAvailable==available);
});
var audioFiles = args.Where(a => !a.StartsWith("--")).ToArray();
if (audioFiles.Length > 0) Test("User-supplied MP3 decoding and private import", () => {
    foreach(var mp3 in audioFiles) {
        Check(AudioPlaybackService.Decode(mp3).Length>0);
        var l=new SoundLibraryService(root); var s=l.ImportAsync(mp3).GetAwaiter().GetResult(); Check(File.Exists(s.Path)); l.Remove(s);
    }
});
if (args.Contains("--audio")) Test("Real Windows output starts, stops and recovers after a missing file", () => {
    using var audio = new AudioPlaybackService();
    using var played = new ManualResetEventSlim(); using var error = new ManualResetEventSlim();
    string? message = null;
    Check(SpinWait.SpinUntil(() => audio.OutputInitializations == 1, 5000), "Output was not initialized at service startup");
    audio.Volume = .2f; audio.Played += () => played.Set(); audio.Error += s => {message=s; error.Set();};
    audio.Play(Path.Combine(root,"absent.wav"),PlaybackMode.RestartSound);
    Check(error.Wait(5000),"Missing-file error not delivered");
    message=null; audio.Play(wav,PlaybackMode.RestartSound);
    Check(played.Wait(5000),message??"Windows output did not start");
    Thread.Sleep(200); Check(message==null,message??""); audio.Stop();
    Check(audio.OutputInitializations==1,"Output recreated on playback");
    long before=audio.TotalPlayRequests;
    var physical=new KeyPressPolicy();
    for(int i=0;i<20;i++) {
        played.Reset();
        if(physical.Process(65,true,false,false))audio.Play(wav,PlaybackMode.AllowOverlap);
        Check(played.Wait(5000),"Request was not played");
        physical.Process(65,false,false,false);Thread.Sleep(140);
        if(i==9)Check(audio.TotalPlayRequests-before==10,"Ten presses generated duplicate requests");
    }
    Check(audio.OutputInitializations==1,"20 plays recreated the output");
    Console.WriteLine("  Injected policy events: ten presses = 10 Play calls; twenty presses = 1 output initialization");
    audio.OnDeviceAdded("unrelated-input-device");
    played.Reset();audio.Play(wav,PlaybackMode.RestartSound);Check(played.Wait(5000));
    Check(audio.OutputInitializations==1,"Unrelated device notification restarted output");
    played.Reset(); audio.OnDefaultDeviceChanged(NAudio.CoreAudioApi.DataFlow.Render,NAudio.CoreAudioApi.Role.Console,"test-default-change");
    audio.Play(wav,PlaybackMode.RestartSound); Check(played.Wait(5000),"Device reset failed");
    Check(audio.OutputInitializations==2,"Selected output change did not recreate output exactly once");
    Console.WriteLine("  " + audio.Diagnostics.Replace("\n", " | "));
});
if (args.Contains("--startup")) Test("Per-user startup roundtrip restores the original registry value", () => {
    using var key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
    var original=key.GetValue("FunnyKeys");
    var kind=original==null ? Microsoft.Win32.RegistryValueKind.String : key.GetValueKind("FunnyKeys");
    try { StartupService.SetEnabled(true); Check(StartupService.IsEnabled()); StartupService.SetEnabled(false); Check(!StartupService.IsEnabled()); }
    finally { if(original==null) key.DeleteValue("FunnyKeys",false); else key.SetValue("FunnyKeys",original,kind); }
});
if (args.Contains("--ui")) Test("WPF window, commands, persistence, tray lifecycle and rendered layout", () => {
    Exception? failure=null;
    var thread=new Thread(() => {
        try {
            var app=new FunnyKeys.App(); app.InitializeComponent();
            using var vm=new FunnyKeys.ViewModels.MainViewModel(Path.Combine(root,"ui"));
            var window=new FunnyKeys.MainWindow(vm); window.Show(); window.UpdateLayout();
            Check(vm.Sounds.Count>=12 && !vm.Enabled && vm.SelectedSound!=null);
            vm.ToggleCommand.Execute(null); Check(vm.Enabled); vm.Volume=23; vm.RandomMode=true; vm.ChaosMode=true; vm.PlaybackIndex=1; vm.RepeatIndex=1; vm.MinimumInterval=0;
            vm.Save(); var saved=new SettingsService(Path.Combine(root,"ui")).Load();
            Check(saved.Enabled && saved.Volume==23 && saved.Repeat && saved.RandomMode && saved.ChaosMode && saved.MinimumInterval==0);
            using(var tray=new TrayIconService(vm,window.Restore)) { window.Hide(); Check(!window.IsVisible); window.Restore(); Check(window.IsVisible); }
            vm.Enabled=false; vm.MinimizeToTray=false;
            vm.Volume=75; vm.RandomMode=false; vm.ChaosMode=false;
            window.Dispatcher.Invoke(() => {}, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            string artifact=Path.GetFullPath("dist/FunnyKeys-preview.png"); Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(window); var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using(var output=File.Create(artifact)) encoder.Save(output);
            using(var diagnosticAudio=new AudioPlaybackService())
            using(var diagnosticHook=new GlobalKeyboardHookService())
            {
                var diagnosticWindow=new FunnyKeys.Views.AudioDiagnosticsWindow(diagnosticAudio,diagnosticHook,()=>vm.SelectedSound){Owner=window};
                diagnosticWindow.Show();diagnosticWindow.UpdateLayout();
                diagnosticWindow.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var diagnosticBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)diagnosticWindow.ActualWidth,(int)diagnosticWindow.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
                diagnosticBitmap.Render(diagnosticWindow);
                var diagnosticEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();diagnosticEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(diagnosticBitmap));
                using(var output=File.Create(Path.GetFullPath("dist/FunnyKeys-audio-diagnostics.png")))diagnosticEncoder.Save(output);
                diagnosticWindow.Close();Check(!diagnosticAudio.DiagnosticsEnabled);
            }
            window.IsExiting=true; window.Close(); app.Shutdown();
        } catch(Exception e) {failure=e;}
    });
    thread.SetApartmentState(ApartmentState.STA); thread.Start(); Check(thread.Join(15000),"UI test timed out"); if(failure!=null) throw failure;
});
int failures=0;
AudioQualityChecks.Register(tests, root, audioFiles);
if (args.Contains("--quality-only")) tests.RemoveAll(t => !t.Name.StartsWith("Audio quality:"));
foreach(var test in tests) { try { test.Run(); Console.WriteLine("PASS " + test.Name); } catch(Exception e) { failures++; Console.WriteLine("FAIL " + test.Name + ": " + e); } }
// Only remove the uniquely generated test directory, never an argument or user-selected path.
Directory.Delete(root,true);
Console.WriteLine($"{tests.Count-failures}/{tests.Count} checks passed");
return failures==0 ? 0 : 1;



