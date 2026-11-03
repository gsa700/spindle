// Spindle — how the app's windows are drawn on Linux: a native Wayland client,
// or through X11 (XWayland). Each app decides it its own way before any window
// exists (Program.NativeWayland: AlbumWall also asks GNOME and whether a screen
// reader runs, Marginal its own setting) and says so here, first thing in Main:
//
//     WindowSystem.NativeWayland = NativeWayland;
//
// WaylandShell, WindowPlacement and DrawnCaptions read it.

namespace Spindle.Ui;

public static class WindowSystem
{
    /// True when the app runs as a native Wayland client (Linux only).
    public static bool NativeWayland { get; set; }
}
