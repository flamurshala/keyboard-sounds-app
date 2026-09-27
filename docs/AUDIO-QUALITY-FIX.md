# FunnyKeys 1.0.2 audio simplification report

This replaces the 1.0.1 DSP approach. The user reports audible fluttering; no recording was attached to this request. No claim is made that an agent listened to physical speakers or headphones.

## Requested results

1. Minimal direct AudioFileReader -> WaveOutEvent: ran to completion without a Windows playback error. Listening quality unverified.
2. CachedSound -> WaveOutEvent: ran to completion. Standard decode/channel conversion/WDL resampling and cached samples compare exactly. Listening quality unverified.
3. NAudio MixingSampleProvider single voice: ran to completion. At master 25%, 75%, and 100%, output equals converted source * 0.70 * master exactly, with no delayed/inserted samples or clamp hits. Listening quality unverified.
4. Original audible distortion's first stage: not established without the recording/listening. The requested suspect DSP has been removed, not patched. A byte-backed WaveBuffer copy error introduced in the new direct-cache diagnostic was found, fixed with Buffer.BlockCopy, and regression-tested; it is not evidence about the original defect.
5. CaptureTransition: deleted, including all old waveform capture, crossfade, ramps and bridge samples. Restart removes all mixer inputs then creates a new provider.
6. LookAheadLimiter: source file deleted. No limiter/look-ahead/compressor/attack/release/EQ/normalization exists in the playback pipeline. Only a final hard clamp remains.
7. Duplicate triggers: none found in code inspection or policy tests. There is one native hook and one ViewModel subscription. WM_KEYDOWN and WM_SYSKEYDOWN pass through the same held-key policy. Save/UI timers do not play sounds; diagnostic playback loops were removed too.
8. Ten simulated physical down/up pairs -> exactly 10 AudioPlaybackService.Play requests. An additional 200 held-key repeat events produce no extra allowed triggers. This is automated policy injection, not a human pressing ten keys.
9. Twenty simulated physical down/up pairs through the real Windows output -> initialization count remains 1. The test subsequently simulates a selected output-device change and confirms exactly one rebuild (count 2). The service opens its output at startup, before the first key press.
10. Internal sample rate: 48000 Hz, stereo LRLR interleaved float32. Mono duplicates to stereo; WDL performs real resampling; unsupported layouts are rejected.
11. Backend: WaveOutEvent through the Windows default output mapper. Observed endpoint: Speaker/HP (Realtek High Definition Audio), Windows mix 48000 Hz stereo IEEE float.
12. Desired latency: 80 ms, three buffers. This is the configured buffer target, not a measured end-to-end latency. WASAPI has not been substituted without evidence WaveOut still fails.
13. Built-in Shotgun.wav: peak 0.596087, RMS 0.063181, 52800 interleaved samples. User-imported shotgun MP3 (93b64745709d4a34b3469578124628de.mp3): peak 0.935624, RMS 0.133787, 99160 samples. Both have NaN=0 / infinity=0. The imported file's maximum single-voice output at 100% master is approximately 0.654937. These are different files and are not interchangeable.
14. Maximum simultaneous voices: 6; the seventh is ignored. Each voice has 0.70 * requested gain. The mixer output is divided by active voice count then multiplied by master once. This simple average bounds normal PCM overlap without a limiter; count changes can change overlap loudness. Single-voice playback is constant gain throughout.
15. Exact source/doc files changed are listed below.
16. Release build and automated test results are recorded in dist/audio-simplification-tests.txt and dist/audio-simplification-build.log. The actual B/C/D/E output probes are in dist/audio-stages.txt.

## Files changed

- src/FunnyKeys/Services/VoiceMixer.cs — replace manual summing/DSP with standard NAudio mixer and simple voice admission/gain.
- src/FunnyKeys/Services/CachedSound.cs — new immutable cache and independent provider.
- src/FunnyKeys/Services/LookAheadLimiter.cs — deleted.
- src/FunnyKeys/Services/AudioPlaybackService.cs — cache objects, 48 kHz conversion, persistent startup output, counters, 80 ms buffering, preserve output on invalid files.
- src/FunnyKeys/Models/AppSettings.cs — default AllowOverlap.
- src/FunnyKeys/Views/AudioDiagnosticsWindow.cs — clean full-length test, sample stats, manual single-trigger controls.
- src/FunnyKeys/FunnyKeys.csproj — version 1.0.2.
- src/FunnyKeys/MainWindow.xaml — version label.
- installer/FunnyKeys.iss — installer version 1.0.2.
- tests/FunnyKeys.Tests/Program.cs — output reuse/count checks and staged probe entry point.
- tests/FunnyKeys.Tests/AudioQualityChecks.cs — replacement signal-integrity/regression checks.
- tests/FunnyKeys.Tests/AudioStages.cs — isolated sequential direct/cache/mixer/two-voice probes.
- README.md and docs/AUDIO-QUALITY-FIX.md — replace obsolete DSP claims and document remaining listening verification.

Generated artifacts: application, portable ZIP, installer, test/build logs, UI preview PNGs, SHA256 manifest. Original user audio is unchanged.

## Listening sequence still required

A. Open the original source in Windows; user reports this sounds clean.
B. With FunnyKeys closed, run the staged probe below: direct playback, cached playback, one mixer voice, then two mixer voices. All use WaveOutEvent 80 ms / three buffers and retain objects until completion. Each isolated probe disposes before the next starts; production does not create devices per key.
C/D. In FunnyKeys select the same file, open Advanced: Audio diagnostics, and use PLAY CLEAN TEST. Keyboard playback is paused, random/cooldown/restart ignored. The whole sample runs once through the production cache/mixer/output. Controls are temporarily disabled to prevent another test interrupting it.
E. Add overlap voices manually after the clean test completes.
F. Uncheck pause, return to another app, and type slowly then rapidly while watching counters.

Repeat on speakers and headphones. Loudness is intentionally reduced by 0.70; compare waveform/timbre at matched listening loudness. No physical listening or recording comparison has been certified. If only WaveOut sounds distorted, compare shared WASAPI at 80 ms next, using the same source and clean test; do not add DSP.

From the repository (local SDK available):

```powershell
$env:DOTNET_ROOT = "$PWD\.tools\dotnet"
& .\.tools\dotnet\dotnet.exe run --project tests/FunnyKeys.Tests -c Release -- --stages "C:\path\to\sound.wav"
```
