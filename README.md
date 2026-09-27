# FunnyKeys

A native Windows 10/11 x64 soundboard for your keyboard. C# / .NET 8 / WPF with NAudio, a global `WH_KEYBOARD_LL` listener, and an Inno Setup installer. Keyboard sounds start **OFF** on first launch.

## Audio simplification (1.0.2)

The custom restart bridge and look-ahead limiter have been removed. Playback uses immutable 48 kHz stereo caches, independent cursors, NAudio MixingSampleProvider, 0.70 per-voice gain, one master gain, and a persistent WaveOutEvent at 80 ms / three buffers. New settings default to Allow Overlap (six voices maximum). Overlapping voices are averaged to provide headroom; their level changes with the active voice count. No normalization, envelope, compressor or crossfade is used.

Open Advanced: Audio diagnostics (Ctrl+Shift+D) and use **PLAY CLEAN TEST** for a full single-sound comparison with Windows playback. This pauses keyboard triggers and bypasses random/restart/cooldown behavior. See `docs/AUDIO-QUALITY-FIX.md` for measured results and listening limitations. Digital checks do not certify speaker/headphone sound quality.
## Use the app

Run `dist/FunnyKeys-Setup.exe` to install, or extract `dist/FunnyKeys-Portable.zip` and run `FunnyKeys.exe`. Keep the `Assets` directory beside the portable executable. Neither version needs Visual Studio or an installed .NET runtime.

Select a sound, preview it, and choose **ENABLE SOUNDS**. Type in another application to hear it. `Ctrl + Shift + F12` toggles sounds globally; if another app owns that shortcut, FunnyKeys reports the conflict. The shortcut is fixed in this version. Close/minimize sends the app to the tray by default. Double-click the tray icon to restore. Use **Exit FunnyKeys** in the window or tray menu to fully stop it.

Features:
- Twelve original synthesized effects, including Boing, Shotgun, Air horn, Fart, Metal pipe, Quack, Explosion and Arcade win. These are deliberately synthetic interpretations, not recordings from games or memes.
- WAV/MP3 import, preview, selection and confirmed removal. AAC/M4A/FLAC also work where the Windows codecs support them.
- Restart, overlap (at most 6 voices), or ignore-until-finished playback.
- Once per physical press or Windows auto-repeat; optional modifier sounds.
- Random pool checkboxes and Chaos Mode (random sounds and 65–100% of master volume). An empty random pool is silent. With random/chaos off, the selected sound is used.
- Immediate master volume, 0–500 ms cooldown, JSON settings, per-user Windows startup, start minimized and tray controls.
- Dark scalable WPF interface, keyboard navigation, status and nonmodal error messages.

Per-key assignments and configurable hotkeys are not included. The activity indicator reports that sound was triggered without displaying or storing the pressed key. There is no network client, telemetry, clipboard capture or keyboard history.

## Requirements and development setup

- Windows 10/11 x64, an audio output device.
- [.NET 8 SDK for Windows x64](https://dotnet.microsoft.com/download/dotnet/8.0).
- [Inno Setup 6 or 7](https://jrsoftware.org/isdl.php) to compile the installer.
- PowerShell 5.1 or later. Visual Studio 2022 with .NET desktop development is optional.
- Internet access for the first NuGet restore. NAudio is pinned to 2.2.1.

This workspace includes a project-local SDK under `.tools/dotnet` and Inno Setup under `.tools/inno`. Build scripts fall back to these when the tools are not on PATH. `.tools` is not source-controlled or shipped.

From the project root, run the development version:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-dev.ps1
```

With the SDK on PATH, `dotnet run --project .\src\FunnyKeys\FunnyKeys.csproj` also works.

Using this workspace's local SDK instead:

```powershell
$env:DOTNET_ROOT = "$PWD\.tools\dotnet"
$env:DOTNET_CLI_HOME = "$PWD\.tools\cli"
$env:NUGET_PACKAGES = "$PWD\.tools\nuget"
& .\.tools\dotnet\dotnet.exe run --project .\src\FunnyKeys\FunnyKeys.csproj
```

## Release and portable build

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

This cleans the two generated output folders, restores dependencies, runs the test harness, and publishes a self-contained `win-x64` single-file executable (with external sound/icon assets). Outputs:

```text
dist/app/FunnyKeys.exe
dist/FunnyKeys-Portable/FunnyKeys.exe
dist/FunnyKeys-Portable.zip
```

## Installer

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

Output: **`dist/FunnyKeys-Setup.exe`**. Use `-SkipBuild` only when the release publish is already current. For a nonstandard Inno installation set `$env:ISCC_PATH` to the full path of `ISCC.exe`.

The installer defaults to Program Files and therefore requests elevation for that installation option. It also offers a per-user installation without elevation. The app itself always runs as a standard user. Start Menu and optional Desktop shortcuts, upgrade file replacement and an uninstaller are included. Exit the app before upgrading/uninstalling. User settings and custom sounds are retained on uninstall; an autostart entry pointing at this install is removed for the uninstalling user. Other Windows users can disable their own autostart if necessary.

The generated binaries are unsigned. Public distribution should include your own Authenticode signing and release testing on clean Windows 10/11 machines. See `docs/TESTING.md` for measured checks and remaining hardware/manual checks. Review Inno Setup's licensing terms for your intended use.

Validation completed here: 12/12 automated/integration checks, Notepad global-key/hotkey probe, self-contained installed launch and clean per-user uninstall. Detailed evidence and unverified hardware/DPI scenarios are recorded in `docs/TESTING.md`.

## Built-in audio and licensing

Place legally redistributable audio files in **`src/FunnyKeys/Assets/Sounds/`**. WAV and MP3 are recommended. Files are discovered automatically by filename; no code registration is needed. They are copied to `Assets/Sounds/` in the published app. Use unique filenames, and preserve any necessary license/attribution with your distribution.

All twelve bundled WAV files are original synthesized sounds generated by `scripts/generate-assets.ps1`. That script also generates the original key-and-sound-wave icon in 16, 24, 32, 48, 64, 128 and 256 pixel sizes plus `.ico`. No external audio assets are bundled. No additional sounds are required. The three MP3s originally placed at the workspace root are user files, are excluded from the installer, and may be imported for personal use with **Add custom sound**. Their distribution rights have not been assumed.

Regenerate assets (overwrites only these generated assets):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\generate-assets.ps1
```

## Custom sounds and settings

- Custom copies: `%LocalAppData%\FunnyKeys\Sounds\`.
- Settings: `%LocalAppData%\FunnyKeys\settings.json`.
- Only validated mono/stereo audio, at most 15 seconds and 20 MB per file, is imported. Decode is bounded by the same duration to handle misleading headers.
- Original files are never moved or deleted. Removal deletes only the app's private copy, after confirmation.
- Corrupt/missing settings fall back to safe defaults, with sounds disabled. Saves use a temporary file then replacement.
- Missing/replaced audio is checked by the audio worker; errors appear in the window without stopping the keyboard input chain.
- Autostart uses the current user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\FunnyKeys`, without admin rights. Disable it before moving a portable installation; re-enable it from the new location.
- Start minimized applies to normal and Windows startup launches after it is enabled. The tray icon is always available, even when close-to-tray is disabled.

## Architecture and performance

```text
FunnyKeys.sln
src/FunnyKeys/
  App.xaml, App.xaml.cs             startup, lifetime and single-instance guard
  MainWindow.xaml, .xaml.cs         WPF interface, minimize/close behavior
  Models/                          settings and sound descriptors
  ViewModels/MainViewModel.cs       commands, state and immutable audio routing
  Helpers/                         notification and command helpers
  Services/
    GlobalKeyboardHookService.cs   dedicated native message-loop thread
    KeyPressPolicy.cs              transient down/up state and modifier filtering
    AudioPlaybackService.cs        bounded queue, decode cache and device recovery
    VoiceMixer.cs                  bounded voice mixing and output limiter
    SoundLibraryService.cs         discovery, validated import and safe removal
    SettingsService.cs             defensive JSON loading and atomic writes
    StartupService.cs              per-user Run key
    TrayIconService.cs             native notification-area integration
  Assets/Icons/                    original multi-resolution icon
  Assets/Sounds/                   12 original WAV effects
tests/FunnyKeys.Tests/              executable regression test harness
installer/FunnyKeys.iss             Windows setup definition
scripts/                           asset generation, release and installer builds
docs/TESTING.md                     validation record and manual checklist
licenses/                          bundled dependency license texts
dist/                              generated installer and portable outputs
```

The hook always calls `CallNextHookEx`, holds only transient virtual-key down states and emits a parameterless trigger. It never creates characters or sentences. Playback is queued without waiting on the hook/UI thread. A worker decodes and caches 44.1 kHz stereo float samples (128 MB cache budget). A reusable NAudio output and mixer enforce a 12-voice limit, smooth restarts and apply a stereo-linked look-ahead limiter after master gain. Pending triggers are invalidated when disabled. Endpoint changes cause output recreation on the next trigger; unavailable devices are retried with backoff. Imports decode on a worker, not the UI thread.

The hook operates on the user's normal desktop. Windows secure desktops, elevated applications and games/anti-cheat policies can restrict global hooks; universal capture in every game or privilege context cannot be promised. “Continuous” mode follows Windows key-repeat events and the configured minimum interval, not an independent timer per held key.

## Tests

```powershell
dotnet run --project .\tests\FunnyKeys.Tests\FunnyKeys.Tests.csproj -c Release
```

Optional WAV/MP3 file paths after `--` exercise real decoding and private import/removal. Tests use a uniquely named temporary directory and never remove the originals. No test stores typed characters. See `docs/TESTING.md` for the full validation checklist.

## Third-party dependencies

NAudio 2.2.1 (MIT, Mark Heath and contributors), Microsoft .NET 8 / WPF / Windows Forms runtime (Microsoft and contributors), and transitive NuGet runtime components. Windows Forms is used only for the native tray icon/menu; the application UI is WPF. No tray framework, Electron, webview, cloud service or audio download dependency. See `THIRD-PARTY-NOTICES.md` and `licenses/` for bundled notices.

