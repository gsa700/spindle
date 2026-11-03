# Spindle

The code that [AlbumWall](https://github.com/gsa700/albumwall),
[Deadwax](https://github.com/gsa700/deadwax) and
[Marginal](https://github.com/gsa700/marginal) share: one copy to fix and test,
instead of a copy in each app kept in step by hand.

It is not a library anything installs. Each app includes this repository as a
git submodule (`spindle/`), pinned to a commit, and compiles it into its own
program; the apps ship Native AOT, so what a user downloads holds no Spindle
file of its own, and no app depends on another being there. An app takes a
change here when its pin is moved forward and it is released.

## What is here

- **Hymie**, the audio engine AlbumWall plays through and Marginal times
  lyrics with:
  - `src/Spindle.Codecs`: the decoders, plain C# with no native code: FLAC,
    MP3, AAC (LC), ALAC and WAV, with exact seeks and gapless trimming as
    FFmpeg does it.
  - `src/Spindle.Playback`: the engine and an output for each system
    (CoreAudio's AudioQueue on the Mac, WASAPI on Windows, PulseAudio or
    PipeWire on Linux), the equalizer and the spectrum.

- **`src/Spindle.Core`**, the small things every app needs alike:
  `VersionOrder` (how release versions order, pre-releases included),
  `ReleaseFeed` (the Edge and Stable channels, read from GitHub),
  `WebLimits` (what is accepted from MusicBrainz and the Cover Art Archive),
  `AppVersion`, `LogFile` (the console teed to `<app>.log`) and `Diagnostics`
  (the crash report and "Copy diagnostics"). Namespace `Spindle`.

- **`src/Spindle.Install`**: installing, updating and uninstalling on
  Windows, Linux and the Mac: the per-user install and its registration, the
  update check, download, checksum and version checks, and the helper that
  swaps the program while it is closed. Each app says which app it is once at
  startup (`AppInfo.Use`); everything else follows from its name. No UI: the
  dialogs stay in each app.

More moves in from the apps over time: the window frame.

## Checking it

    dotnet test                                                   # tests/Spindle.Tests
    dotnet run tools/engine-check.cs -- compare <file>...         # every sample against ffmpeg's decode
    dotnet run tools/engine-check.cs -- seek <file>               # seeks land on the exact sample

The tests decode short fixtures of each format against ffmpeg's decode of
them, check seeks, a file cut short, a header claiming an impossible rate,
and the equalizer; and they cover Spindle.Core: version order, the release
feed, the web limits, the log and the crash report. `engine-check` does the same on real music.

## License

GNU General Public License, version 3 (LICENSE), as the apps. Some decoders
are ported from other projects; THIRD-PARTY-NOTICES.md says which, at which
commit, under which license, and their license texts are in licenses/.
