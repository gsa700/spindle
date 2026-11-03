// Spindle — is a screen reader running? From AlbumWall (Program.AskScreenReader,
// 2026-10-21), moved here 2026-11-03 so Deadwax and Marginal ask too.
//
// Avalonia 12.1 speaks to Linux screen readers (AT-SPI) only from its X11
// backend: Avalonia.Wayland registers nothing, so on native Wayland Orca cannot
// see the app at all (Techbench, Fedora 45, 2026-11-03: Marginal and Deadwax
// 0 of 3 starts on the accessibility bus native, all through XWayland). Each
// app goes through XWayland when this says yes:
//
//     public static readonly bool NativeWayland = ... && !ScreenReader.Running;

using System.Diagnostics;

namespace Spindle;

public static class ScreenReader
{
    /// A screen reader is running as the app starts (Linux only; false
    /// elsewhere, where the platform's own accessibility works). Asked once,
    /// in a second at most. A property, not a field: an app's NativeWayland
    /// static field reads it during the app's own static initialization.
    public static bool Running => _running ??= OperatingSystem.IsLinux() && Ask();
    private static bool? _running;

    /// The answer from what the system said. Three ways to know, any one
    /// enough: Orca's own process; GNOME's "screen reader" switch; the
    /// accessibility bus's IsEnabled, which an assistive tool sets when it
    /// starts. at-spi2-core 2.62 (Fedora 45) has no ScreenReaderEnabled any
    /// more, which AlbumWall's first version asked for and so never saw Orca.
    public static bool Decide(bool orcaRunning, string gsettingsScreenReader, string busIsEnabled)
        => orcaRunning
        || gsettingsScreenReader.Trim() == "true"
        || busIsEnabled.Trim() == "b true";

    private static bool Ask()
    {
        if (Process.GetProcessesByName("orca").Length > 0) return true;
        return Decide(false,
            Run("gsettings", "get org.gnome.desktop.a11y.applications screen-reader-enabled"),
            Run("busctl", "--user get-property org.a11y.Bus /org/a11y/bus org.a11y.Status IsEnabled"));
    }

    private static string Run(string tool, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(tool, args)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
            });
            if (p is null || !p.WaitForExit(1000)) { try { p?.Kill(); } catch { } return ""; }
            return p.StandardOutput.ReadToEnd().Trim();
        }
        catch
        {
            return "";      // no such tool here: nothing it could tell us
        }
    }
}
