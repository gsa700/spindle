// Spindle — how the app's windows are drawn on Linux: a native Wayland
// client, or through X11 (XWayland). Each app decides before any window exists
// (Program.NativeWayland: native Wayland wherever a Wayland session exists,
// X11 only with no Wayland session or while a screen reader runs, see
// Spindle.ScreenReader) and says so here, first thing in Main:
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
