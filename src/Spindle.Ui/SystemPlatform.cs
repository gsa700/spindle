// Spindle — Avalonia's platform for this system, in one call the apps make
// where they had UsePlatformDetect (2026-11-07):
//
//     AppBuilder.Configure<App>().UseSystemPlatform()
//
// UsePlatformDetect knows Windows, macOS and Linux and configures nothing on
// FreeBSD ("No runtime platform services configured" when the window opens).
// There the apps run on X11, drawn by Skia, with HarfBuzz shaping text: what
// detection picks on Linux under X11. Native Wayland is not offered on FreeBSD.
// Avalonia's X11 code imports Linux's epoll from "libc", which
// Spindle.NativeLoading sends to the system's libepoll-shim there.

using Avalonia;

namespace Spindle.Ui;

public static class SystemPlatform
{
    public static AppBuilder UseSystemPlatform(this AppBuilder builder) =>
        OperatingSystem.IsFreeBSD()
            ? builder.UseX11().UseSkia().UseHarfBuzz()
            : builder.UsePlatformDetect();
}
