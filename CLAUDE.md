# Working in this repository

This repository is PUBLIC (since 2026-11-02). Everything committed and pushed
here is published: it can be copied, and a force-push does not fully take it
back. The rules are AlbumWall's, Deadwax's and Marginal's.

## Privacy: before every commit and every push

1. **Author email is the GitHub no-reply address**, never a personal one. In
   every clone, on every machine, before the first commit:

       git config user.email "298062495+gsa700@users.noreply.github.com"
       git config user.name  "David Erickson"

   Check with `git log -1 --format='%ae %ce'` after committing. A clone made
   by `git submodule update` inside an app is a clone too: set it there.
2. **No private network details in any file or commit message**: no LAN IP
   addresses (10.x, 192.168.x, 172.16-31.x), no share paths built on them.
   Use the machine's NAME instead (Techbench, Hambench, NASBOX are fine).
3. **No personal email, keys, tokens or passwords** anywhere.
4. **Scan before pushing**:

       git log origin/main..HEAD --format='%ae%n%ce' | sort -u      # only the no-reply address
       git diff origin/main..HEAD | grep -nE '\b(10(\.[0-9]{1,3}){3}|192\.168(\.[0-9]{1,3}){2}|172\.(1[6-9]|2[0-9]|3[01])(\.[0-9]{1,3}){2})\b'

## What this is

The code the three apps share, included in each as a git submodule at
`spindle/` and compiled into each app (README.md). Nothing here is released
on its own: no version, no tags needed. An app takes a change by moving its
pin (a commit in the app: `git -C spindle pull` then commit the `spindle`
change) and releasing.

- **A change here is a change to every app that pins it.** Run the tests
  here, then build and test each app on the new pin before moving it.
- **Nothing app-specific**: no app's name, paths or settings in Spindle's
  code. What differs is passed in by the app, or read from the running
  program (PulseOutput names the stream after the program, for example).
- **Native AOT**: every project is `IsAotCompatible`; a trim or AOT warning
  here is a warning in every app.
- **Every project stays independent of the apps**: Directory.Build.props here
  stops an app's own one from reaching in.

## Where things came from

- `src/Spindle.Codecs` and `src/Spindle.Playback`: Hymie, moved from AlbumWall
  (`src/AlbumWall.Codecs` and `src/AlbumWall.Playback` at 8f07680, AlbumWall
  0.8.9) on 2026-11-02; Marginal's copy (from 2026-10-31) was the same code
  less the equalizer and EndAfterCurrent. `tools/engine-check.cs` came with
  it. Their history before that is in AlbumWall's repository.
- `src/Spindle.Core` (2026-11-02): VersionOrder and ReleaseFeed (the same
  code in all three apps), WebLimits and AppVersion (Deadwax's, copied to
  Marginal), LogFile and Diagnostics (AlbumWall's, copied to the other two).
  What was app-specific is now handed in: `LogFile.Start(folder, name)` and
  `Diagnostics.CatchCrashes(name, version, display)`; the UI thread's handler
  stays in each app. Their tests came from Deadwax and Marginal
  (VersionOrderTests, ReleaseFeedTests, the WebLimits ones from Deadwax's
  SecurityTests); DiagnosticsTests are new.
- In `src/Spindle.Core` too (2026-11-02, for Deadwax's lyrics at rip time):
  LrcLib (LrcLibClient: lookup, retries, Fit/Rank, the 2 s rule), Lrc (LRC read,
  write, timing by hand; LyricsKind and KindOf, from Marginal's TrackReader)
  and Json (from Marginal.Core; it replaced Spindle.Install's JsonText). The
  client takes a userAgent; without one, the program's name and version.
  Tests: LrcTests, LrcLibTests (from Marginal's LyricsTests, LrcLibRetryTests
  and ParsingSecurityTests).
- `src/Spindle.Install` (2026-11-02): the install and update code, from
  Deadwax by way of Marginal (whose copy had its 2026-10-31 hardening: download
  size and stall limits, the staged program must say its version, Stable takes
  only app tags, the redirect must land on GitHub; and Windows-only `/switch`
  parsing). Every app name, repository, bundle id, folder and file name is
  AppInfo's now (AppInfo.cs). ToolPath and JsonText are internal, so the apps'
  own ToolPath does not clash. Tests: UpdateApplyTests, InstallSecurityTests
  and InstallCommandLineTests run as a made-up "TestApp" (TestAppInfo.cs); the
  Mac update tests stay in each app, since they check the real notarized
  /Applications/<App>.app.
- `src/Spindle.Ui` (2026-11-02, the window step's first part): ConsoleLogSink
  (identical in all three), WaylandShell (all three, app id now AppInfo's),
  WindowPlacement (Deadwax's, copied to Marginal), WindowButtons and
  DrawnCaptions (AlbumWall's, in their newest form, which Deadwax lacked:
  screen-reader names, no guessed gsettings). Each app sets
  WindowSystem.NativeWayland in Main; how it decides stays its own. Avalonia's
  version here must match the apps'.
- `TextSize` and `MacWindow` in Spindle.Ui (2026-11-02, the window step's
  second part). TextSize merges AlbumWall's/Deadwax's copy (follows content a
  window puts in later) with Marginal's (wraps at once so a drawn frame goes
  outside the scale; smallest sizes held to the screen; a maximized window not
  resized; Factor): it follows new content except a frame that holds the scale.
  MacWindow is AlbumWall's whole (the others' copy was it without HideTrafficLights,
  Nudge, WatchDisplays and the remembered bar height); its Objective-C class is
  SpindleDisplayWatcher.
- From AlbumWall's install and update code, 2026-11-02, when AlbumWall moved
  onto Spindle.Install (its scripts were already the same): the supervised
  update for a kiosk (<APP>_SUPERVISED=1: the app puts the new build in place
  itself, the old one in previous/, and the launcher restarts it; tested in
  SupervisedUpdateTests), the refusal to install under a relative or
  control-character path (RequireProgramsDirectory), the optional
  ~/.local/bin/<app> link (AppInfo.CommandLink), and Uninstall removing
  settings' .bak copies and an emptied unpacking folder.
- `ScreenReader` in Spindle.Core (2026-11-03): AlbumWall's AskScreenReader
  (2026-10-21). Avalonia 12.1 has AT-SPI only in its X11 backend, so on native
  Wayland a screen reader cannot see an app; each app's NativeWayland adds
  `&& !ScreenReader.Running` and goes through XWayland instead. Deadwax and
  Marginal lacked it (Techbench, 2026-11-03). Tests: ScreenReaderTests.
- `DrawnFrame` in Spindle.Ui (2026-11-03, the window step's third part):
  Marginal's whole (made from Deadwax's frame, itself AlbumWall's: corners,
  shadow, hairline, window buttons, resize band, bar drag and double click;
  the Mac keeps its own title bar). Its colours are the app's resources by
  name (GroundKey, SoftKey, EdgeKey; Marginal's names by default). Deadwax
  keeps its own frame inside its MainWindow.
- License files on install and update (2026-11-03): `InstallLayout.CopyLicenses`
  puts LICENSE, THIRD-PARTY-NOTICES.md and licenses/ from beside the program
  (a release zip has them) into the installed copy, on install and on every
  Linux/Windows update (in C#, before the helper starts; the helpers still move
  only the program and its libraries). Before, no installed copy had them
  (Techbench, Mobilenode). Tests: LicenseCopyTests. The Mac bundle carries them
  in Contents/Resources.
- `NativeLoading` in Spindle.Core (2026-11-05): Marginal's NativeLoading
  (security review 2026-10-31, C2), made app-neutral for AlbumWall and
  Deadwax. Libraries other than those an app ships beside itself (AppInfo's
  NativeLibraries, handed to Install) come from the system's places first, for
  Spindle's and Avalonia's assemblies and those the app names; the Mac is left
  to library validation. An assembly with its own resolver (AlbumWall.Domain's
  libFLAC) calls Resolve/FromSystem from it. Tests: NativeLoadingTests.
- `OssOutput` in Spindle.Playback (2026-11-07): Hymie's output on FreeBSD,
  through OSS (/dev/dsp, the kernel's sound(4)); no daemon or library to
  install, unlike PulseAudio, which a stock FreeBSD desktop lacks. 32-bit
  integer samples, the kernel's virtual channels mix it with other programs
  (bit-perfect mode, if a user turns it on, makes the device exclusive);
  pause, clock and end work as in PulseOutput. Checked on Hubble (FreeBSD
  15.1, Realtek ALC235): engine-check play, and AlbumWall heard through the
  headphones, pausing and playing beside Firefox.
- FreeBSD platform support (2026-11-07): `SystemPlatform.UseSystemPlatform()`
  in Spindle.Ui, which the apps call where they had UsePlatformDetect (that
  configures nothing on FreeBSD; there it is X11, Skia and HarfBuzz; Spindle.Ui
  references Avalonia.Desktop for it, at the apps' version), and in
  NativeLoading, "libc" answered on FreeBSD by the system's libepoll-shim.so.0
  (Avalonia's X11 code imports Linux's epoll from "libc"; the shim needs
  libc.so.7, so every other libc function is found through it). Checked on
  Hubble with AlbumWall installed and nothing added by hand. Tests:
  NativeLoadingTests.
- `tools/build-libskiasharp.sh` (2026-11-07): builds libSkiaSharp.so for
  FreeBSD for a given SkiaSharp version (SkiaSharp ships none; the
  graphics/libskiasharp port lags). The Skia commit comes from the NuGet
  package's own source commit (its nuspec), then mono/SkiaSharp's
  externals/skia at that commit; the port's patches are applied, skipped when
  Skia already has them, and stop the build when they neither apply nor are
  known to be superseded; only the gn switches that Skia declares are passed.
  BUILD-INFO.txt records the commits, the patches and a sha256. Checked on
  Hubble: 3.119.4 (Skia 119, 812 sk_ exports, the same as the hand build the
  FreeBSD AlbumWall ran on) and 4.155.0 (Skia 155, 877 exports; a SkiaSharp
  4.155 program drew and encoded a PNG through it).
