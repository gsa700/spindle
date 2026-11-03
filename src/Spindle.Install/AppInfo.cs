// Spindle — which app this is, for the install and update code.
//
// The install and update code is the same in every app (it came from Deadwax,
// was copied to Marginal and hardened there, and moved here 2026-11-02); what
// differs is only which app it is. Each app says so once, first thing in Main:
//
//     AppInfo.Use(new AppInfo { Name = "Marginal", Description = "...", DataDir = ...,
//                               AppAssembly = typeof(Program).Assembly });
//
// Everything else follows from the name, by the conventions all three apps keep:
// the repository gsa700/<name>, the bundle io.github.gsa700.<Name>, the Windows
// app id gsa700.<Name>, the folders, the log, the .desktop file.

using System.Reflection;

namespace Spindle.Install;

public sealed class AppInfo
{
    /// The app's name as people see it, and its program's ("Marginal"): the
    /// window class, the Windows install folder, the .app, the process name.
    public required string Name { get; init; }

    /// One line for the installed-apps list and the .desktop entry.
    public required string Description { get; init; }

    /// Where the app keeps its settings (the folder of settings.json).
    public required string DataDir { get; init; }

    /// The app's own assembly (typeof(Program).Assembly): its version is the
    /// one the update check compares, and its app-icon.png the one installed.
    /// Not Spindle's, which carries no app version (found 2026-11-02 before
    /// release: every copy said it was 1.0.0, and none would have updated).
    public required Assembly AppAssembly { get; init; }

    /// The .desktop entry's categories (freedesktop.org menu spec).
    public IReadOnlyList<string> DesktopCategories { get; init; } = ["AudioVideo", "Audio"];

    /// The native libraries a release carries beside the program; the same
    /// list as the app's release.sh and install.sh (they must agree).
    public IReadOnlyList<string> NativeLibrariesWindows { get; init; } = ["libSkiaSharp.dll", "libHarfBuzzSharp.dll", "av_libglesv2.dll"];
    public IReadOnlyList<string> NativeLibrariesUnix { get; init; } = ["libSkiaSharp.so", "libHarfBuzzSharp.so"];

    /// Files in DataDir that Uninstall removes besides the settings, the
    /// registration log, the log and the crash report (an index, a session).
    public IReadOnlyList<string> MoreDataFiles { get; init; } = [];

    /// On Linux, a link ~/.local/bin/&lt;app&gt; to the installed program, so the
    /// app starts by name from a terminal (AlbumWall's; the others have none).
    public bool CommandLink { get; init; }

    /// Lower case, for Unix folders and file names ("marginal").
    public string Lower => Name.ToLowerInvariant();

    public string Repo => $"gsa700/{Lower}";
    public string BundleId => $"io.github.gsa700.{Name}";
    public string WindowsAppId => $"gsa700.{Name}";

    /// Set to 1 by a kiosk's launcher, which restarts the app when it exits
    /// (UpdateService.Supervised): ALBUMWALL_SUPERVISED, for AlbumWall's Pi.
    public string SupervisedVariable => $"{Name.ToUpperInvariant()}_SUPERVISED";

    /// The environment variable that points the update check at a test feed.
    public string FeedVariable => $"{Name.ToUpperInvariant()}_UPDATE_FEED";

    private static AppInfo? _current;

    /// The app, as AppInfo.Use was told. Asking before then is a mistake in
    /// the app, said plainly rather than guessed around.
    public static AppInfo Current =>
        _current ?? throw new InvalidOperationException("Spindle.Install: AppInfo.Use was not called before the install or update code ran.");

    public static void Use(AppInfo app) => _current = app;
}
