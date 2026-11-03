// Spindle — text size: how large the windows are drawn (Preferences,
// Accessibility). From AlbumWall (2026-11-01), copied to Deadwax and Marginal;
// Marginal's copy changed to fit its drawn frame. The two merged here,
// 2026-11-02.
//
// A larger size is not a font setting: the type sizes are written into the
// windows (11, 12, 13 where the design wants them), so each window is drawn
// through a scale, text, spacing and buttons together, the way a browser's
// zoom works.
//
// THE MERGE, what each copy needed and now both have:
// - The content is wrapped AT ONCE, at a scale of 1 for 100% (Marginal's): a
//   frame the app puts around the window's content after Follow (Marginal's
//   DrawnFrame: shadow margin, resize band, window buttons) goes OUTSIDE the
//   scale and stays its own size.
// - New content is followed (AlbumWall's and Deadwax's): a window that puts
//   new content in later (Properties for the next album, a statistics page
//   rebuilt) has it wrapped in turn; but a frame put AROUND the scaled part is
//   recognised (it holds the scale) and left alone.
// - Smallest sizes are held to the screen too, and a maximized or full-screen
//   window is not resized (Marginal's).
// - Factor, for sizes an app keeps at 100% and opens at the chosen size
//   (Marginal's Settings).
//
// Applies at once to every window that follows it, open or not.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Spindle.Ui;

public static class TextSize
{
    /// The choices, as percentages of the designed size.
    public static readonly int[] Choices = [100, 115, 130, 150];

    public static int Percent { get; private set; } = 100;

    /// The scale for Percent: a size kept at 100% is opened at size × Factor.
    public static double Factor => Percent / 100.0;

    /// Raised after a new size is chosen, once the windows have followed it.
    public static event Action? Changed;

    public static void Choose(int? percent)
    {
        var p = percent is { } v && Choices.Contains(v) ? v : 100;
        if (p == Percent) return;
        Percent = p;
        Console.WriteLine($"[text] {Percent}%");
        Changed?.Invoke();
    }

    /// Once the window's content is in place (the end of its constructor, or
    /// right after InitializeComponent), and before any frame the app draws
    /// around it. The window is drawn at the chosen size from then on and
    /// follows any change until it closes. A window that sizes itself to its
    /// content needs nothing more; one with a size grows or shrinks with the
    /// text, from the size it has now (built, dragged or kept), but never past
    /// the screen it is on.
    public static void Follow(Window window)
    {
        var host = new LayoutTransformControl();
        double minWidth = window.MinWidth, minHeight = window.MinHeight;
        var applied = 1.0;
        var swapping = false;

        // Whether `control` is, or holds, the scale: a frame put around it.
        bool HoldsHost(object? control)
        {
            for (var c = (Control?)host; c is not null; c = c.Parent as Control)
                if (ReferenceEquals(c, control)) return true;
            return false;
        }

        void Wrap(Control content)
        {
            swapping = true;
            try
            {
                window.Content = null;
                host.Child = content;
                window.Content = host;
            }
            finally { swapping = false; }
        }

        if (window.Content is Control first) Wrap(first);

        window.PropertyChanged += (_, e) =>
        {
            if (swapping || e.Property != ContentControl.ContentProperty) return;
            if (e.NewValue is not Control content || HoldsHost(content)) return;
            Wrap(content);
        };

        void Apply()
        {
            var f = Factor;
            // 1 said explicitly at 100%, never null: going from 150% back to
            // 100% otherwise left the old scale on the content, laid out at
            // 150% in a window that had shrunk (AlbumWall, Techbench, 2026-11-01).
            host.LayoutTransform = new ScaleTransform(f, f);
            var area = (window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary) is { } screen
                ? screen.WorkingArea.Size.ToSize(screen.Scaling) : new Size(double.MaxValue, double.MaxValue);
            window.MinWidth = Math.Min(minWidth * f, area.Width - 48);
            window.MinHeight = Math.Min(minHeight * f, area.Height - 48);
            if (window.SizeToContent == SizeToContent.Manual && window.WindowState == WindowState.Normal)
            {
                if (!double.IsNaN(window.Width)) window.Width = Math.Min(Math.Ceiling(window.Width / applied * f), area.Width - 48);
                if (!double.IsNaN(window.Height)) window.Height = Math.Min(Math.Ceiling(window.Height / applied * f), area.Height - 48);
            }
            applied = f;
        }

        Apply();
        Changed += Apply;
        window.Closed += (_, _) => Changed -= Apply;
    }
}
