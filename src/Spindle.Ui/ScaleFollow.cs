// Spindle — a window keeps its size, in its own units, when it moves to a monitor
// with another scale (Vergence Mode B on X11, 2026-11-07).
//
// Avalonia's X11 backend switches a window's scale as it crosses to a monitor with
// another factor, and asks for a window of the same logical size at the new scale.
// While the window manager is moving the window (a drag by the title bar), xfwm4
// does not grant that resize, so the window keeps its pixels, which at the new
// scale are fewer units: a drag from the ASUS (1.07) to the 14" 4K (1.64) took
// AlbumWall from 940 to 613 to 585 units wide, and it never grew back. A window
// that sizes itself to its content is unaffected; one with a size of its own is.
//
// So the size is taken when a drag starts (the first move after a still spell) or
// when the person resizes a still window, every size change while the window moves
// or while this puts it back is ignored, and once the window is still again after a
// move in which the scale changed, that size is put back: fitted to the screen it
// is on, but remembered whole for the way back. X11 only: Windows and macOS resize
// a window for a new scale themselves, and Wayland compositors scale it.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Spindle.Ui;

public static class ScaleFollow
{
    public static void Keep(Window window)
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || WindowSystem.NativeWayland) return;

        Size? kept = null;          // the size the person chose, in the window's units
        var lastMove = 0L;          // Environment.TickCount64 of the last move
        var scaled = false;         // the scale changed during this move
        var restoringUntil = 0L;    // our own resize is not the person's
        Size? fitted = null;        // the size this last set, fitted to a smaller screen
        var settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        bool Moving() => Environment.TickCount64 - lastMove < 350;
        bool Restoring() => Environment.TickCount64 < restoringUntil;
        bool IsFitted(Size s) => fitted is { } f && Math.Abs(s.Width - f.Width) < 2 && Math.Abs(s.Height - f.Height) < 2;

        window.Opened += (_, _) => kept = window.ClientSize;
        window.SizeChanged += (_, e) =>
        {
            if (Moving() || scaled || Restoring()) return;
            if (window.WindowState == WindowState.Normal && !IsFitted(e.NewSize)) { kept = e.NewSize; fitted = null; }
        };
        window.PositionChanged += (_, _) =>
        {
            if (!Moving() && !scaled && !Restoring() && window.WindowState == WindowState.Normal && !IsFitted(window.ClientSize))
                kept = window.ClientSize;
            lastMove = Environment.TickCount64;
            settle.Stop();
            settle.Start();
        };
        window.ScalingChanged += (_, _) =>
        {
            scaled = true;
            settle.Stop();
            settle.Start();
        };
        settle.Tick += (_, _) =>
        {
            settle.Stop();
            if (!scaled) return;
            scaled = false;
            if (kept is not { } k || window.WindowState != WindowState.Normal) return;
            var screen = window.Screens.ScreenFromWindow(window);
            var area = screen is null ? new Size(double.MaxValue, double.MaxValue)
                                      : screen.WorkingArea.Size.ToSize(screen.Scaling);
            var w = Math.Min(k.Width, area.Width);
            var h = Math.Min(k.Height, area.Height);
            if (Math.Abs(window.ClientSize.Width - w) < 1 && Math.Abs(window.ClientSize.Height - h) < 1) return;
            Console.WriteLine($"[window] scale {window.RenderScaling}: back to {w:0}x{h:0} (kept {k.Width:0}x{k.Height:0})");
            restoringUntil = Environment.TickCount64 + 600;
            fitted = w < k.Width || h < k.Height ? new Size(w, h) : null;
            window.Width = w;
            window.Height = h;
        };
    }
}
