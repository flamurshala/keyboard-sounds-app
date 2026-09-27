# Validation record

Development environment: Windows x64, project-local .NET SDK 8.0.425, runtime 8.0.31, NAudio 2.2.1, Inno Setup 6.7.3.

Final extended run: **12/12 checks passed**. Release/publish completed with zero compiler warnings/errors; Inno Setup compiled successfully.

## Installer and packaged application

- Generated `dist/FunnyKeys-Setup.exe` and `dist/FunnyKeys-Portable.zip`.
- Installed using the generated EXE in per-user mode to `dist/install-smoke`; installer exit code 0.
- Confirmed the executable, 12 audio files, external icon, Start Menu shortcut and uninstall registration were installed.
- Launched the installed single-file application with `DOTNET_ROOT` cleared and no system .NET installation. The real window opened with sounds OFF and all 12 library entries present.
- Reopening the executable restored the existing instance; only one FunnyKeys process remained.
- Used keyboard navigation to focus **Exit FunnyKeys**, activated it, and confirmed the process exited normally.
- Ran the generated uninstaller; exit code 0. The installed executable and uninstall registration were removed. Test installation logs remain in `dist/install-smoke.log` and `dist/uninstall-smoke.log`.
- The temporary test installation is not left installed. Deliverable EXE/ZIP files remain in `dist/`.

Program Files / UAC installation on a separate clean machine remains a manual acceptance check; the non-elevated per-user installation/uninstallation path was exercised here.

## Automated regression checks

The console harness covers independent behavior, not just property assignments:

- Transient physical key state, concurrent held keys, release/repress, repeat, modifiers and special keys.
- Restart replaces the voice, ignore preserves it, overlap caps at 16, finished voices are removed, master-volume clamp and stop clear the mixer.
- JSON roundtrip, range and enum normalization, missing/null collections and corrupt-settings recovery.
- Mono WAV decoding/resampling to 44.1 kHz stereo, missing and malformed files, size and decoded-duration limits.
- Validated custom-copy import and removal without modifying the original; built-ins cannot be deleted; traversal in settings is rejected.
- Native hook creation/disposal and release of the global hotkey registration.
- Optional user-file checks successfully decode/import/remove private copies of all three supplied MP3s. The originals are untouched and are not distributed.
- Real Windows output initialization, start/stop, missing-file recovery and simulated endpoint-change recovery.
- WPF window creation, command execution, settings roundtrip, tray icon creation/disposal, hide/restore and an actual WPF render saved to `dist/FunnyKeys-preview.png`.
- Optional startup registration check preserves and restores the original Run value in `finally`.

Run the base suite:

```powershell
dotnet run --project tests/FunnyKeys.Tests -c Release
```

Run UI/audio/startup integration checks on an interactive Windows desktop (brief test sound at 20% volume):

```powershell
dotnet run --project tests/FunnyKeys.Tests -c Release -- --audio --ui --startup
```

Add paths after `--` to test user audio files. The test harness's data is isolated in a uniquely named temporary directory. It deletes only that directory and its own imported copies.

## Cross-application keyboard probe

The native hook was tested from an interactive, non-sandboxed desktop process while Notepad was focused. A Space press reached Notepad and produced one global sound trigger and one Windows audio submission. Ctrl+Shift+F12 produced one global toggle. No characters or key identifiers were recorded by the probe; it reports only aggregate counters.

The same test from the tool sandbox received no desktop input, so interactive hook tests must be run outside that isolated context. This is a test-host limitation, not a request for the distributed app to run as administrator.

Reproduce with:

```powershell
dotnet run --project tests/FunnyKeys.Tests -c Release -- --listen
```

During the 45-second probe, press a key in Notepad and the toggle chord. It prints aggregate trigger/audio/toggle counts, disposes the hook/audio services, and exits.

## UI observations and remaining release checks

The real application launched and its accessibility tree exposed the master switch, volume, library previews, import/removal, timing settings and startup settings. In-process rendering was visually inspected and used to correct display-name and foreground-contrast issues.

The desktop screenshot helper fails here with `SetIsBorderRequired failed: No such interface supported (0x80004002)`; mouse operations that need capture geometry consequently fail. Keyboard automation and accessibility reads do work. Therefore full mouse-driven interaction, physical held-key timing, visual DPI checks at 125/150/200%, audible listening quality, actual headphone unplug/replug, real sign-in autostart, arbitrary games and clean-machine Windows 10/11 compatibility still require manual verification. Do not describe these as completed tests.

Manual acceptance checklist:

1. Install, launch, confirm sound defaults OFF and Boing is selected.
2. Preview effects, choose a sound, enable and type in Notepad/Chrome/Discord.
3. Hold/release/repress a key in once and continuous modes; try arrows, Enter, Space, function keys and modifiers.
4. Test all playback modes with fast typing, interval 0/50/500 ms, volume 0/75/100.
5. Import WAV and MP3 through the picker, preview/select, remove and confirm the original survives.
6. Try random/chaos with one, several and no pool entries.
7. Minimize/close to tray, restore, use tray sound selection and toggle, then explicitly exit.
8. Restart, confirm persistence and disabled state; test start-minimized and actual Windows sign-in startup.
9. Remove/replace a custom sound externally, corrupt settings while closed, disconnect/reconnect headphones.
10. Test 100/125/150/200% scale, small displays and keyboard-only focus navigation.
11. Install to Program Files through the elevated option on a clean PC; run as a standard user; upgrade and uninstall.

The executable and installer are unsigned; code-signing and clean-machine acceptance remain distribution tasks.
