// Where the system's native libraries are looked for.
//
// A [DllImport("libpulse-simple.so.0")] by a bare name is looked for in the
// program's own folder first. A copy run from where it was unpacked, beside
// whatever else is there (Downloads), would load a library of that name
// planted beside it: Marginal's security review ran a planted
// libpulse-simple.so.0 through a Native AOT build (2026-10-31, C2). Avalonia's
// X11, Wayland, OpenGL and Windows imports (libX11.so.6, libEGL.so.1,
// dwmapi.dll...) and an app's own (libc, kernel32) work the same way. The
// release zips unpack into a folder of their own; this is the second half:
//
//   * The libraries an app ships beside itself (Skia, HarfBuzz, ANGLE, SQLite:
//     AppInfo's NativeLibraries) are loaded as before, from its folder.
//   * Every other library the steered assemblies ask for is looked for in the
//     system's places first (dlopen's own search on Linux, System32 on
//     Windows), so a system library always wins over a file beside the program.
//   * Only a library the system does not have at all falls back to the
//     default search, as before: nothing that worked stops working.
//
// The Mac needs none of this: the hardened runtime's library validation
// refuses any library not signed by Apple or by Dave's Developer ID.
//
// A .NET assembly takes one resolver. An assembly that already sets its own
// (AlbumWall.Domain's libFLAC) is left out of Install and calls Resolve or
// FromSystem from its resolver instead.

using System.Reflection;
using System.Runtime.InteropServices;

namespace Spindle;

public static class NativeLoading
{
    /// The assemblies every app steers: Spindle's own, and every assembly of
    /// Avalonia's that imports a system library (read from the apps' builds,
    /// Avalonia 12.1, 2026-11-05; NWayland holds libwayland-client's imports,
    /// and a planted copy of it was loaded until it was here). One that is not
    /// part of a build is passed over. Left out: Avalonia.FreeDesktop.AtSpi,
    /// which sets its own resolver (libdbus), and SkiaSharp and HarfBuzzSharp,
    /// whose libraries ship beside the program.
    public static readonly IReadOnlyList<string> SharedAssemblies =
    [
        "Spindle.Playback", "Spindle.Install", "Spindle.Ui",
        "Avalonia.Base", "Avalonia.X11", "Avalonia.Wayland", "NWayland", "Avalonia.FreeDesktop",
        "Avalonia.OpenGL", "Avalonia.Skia", "Avalonia.Win32", "Avalonia.Win32.Automation", "Tmds.DBus.Protocol",
    ];

    private static string[] _beside = [];

    /// Steers the shared assemblies and <paramref name="appAssemblies"/>.
    /// <paramref name="besideLibraries"/> are the file names the app ships
    /// beside its program (AppInfo.NativeLibrariesUnix and ...Windows). Call it
    /// first thing in Main, before anything loads a library.
    public static void Install(IEnumerable<string> besideLibraries, params string[] appAssemblies)
    {
        _beside = [.. besideLibraries.Select(Stem).Distinct(StringComparer.OrdinalIgnoreCase)];
        if (OperatingSystem.IsMacOS()) return;
        foreach (var name in SharedAssemblies.Concat(appAssemblies))
        {
            try { NativeLibrary.SetDllImportResolver(Assembly.Load(name), Resolve); }
            catch (Exception e) when (e is FileNotFoundException or FileLoadException or InvalidOperationException or ArgumentException) { }
        }
    }

    /// A handle from the system's own places, or zero for the default search
    /// (a shipped library, a library the system lacks, or any library on a Mac).
    public static IntPtr Resolve(string library, Assembly assembly, DllImportSearchPath? searchPath) =>
        IsBeside(library, _beside) ? IntPtr.Zero : FromSystem(SystemName(library, OperatingSystem.IsFreeBSD()), assembly);

    /// The system library that answers for <paramref name="library"/>. On
    /// FreeBSD, "libc" is the epoll-shim package's library: Avalonia's X11 code
    /// imports Linux's epoll_create1, epoll_ctl and epoll_wait from "libc", and
    /// FreeBSD has kqueue instead. libepoll-shim.so.0 has those, and needs
    /// libc.so.7, so every other function asked of "libc" is found through it
    /// too (a handle's lookup searches what it depends on). Not
    /// libepoll-shim-interpose, which also takes over read and close for every
    /// descriptor. The apps' FreeBSD package depends on devel/libepoll-shim.
    internal static string SystemName(string library, bool freeBsd) =>
        freeBsd && library == "libc" ? "libepoll-shim.so.0" : library;

    /// The library from the system's places only, never the program's folder;
    /// zero when the system has none (and always on a Mac).
    public static IntPtr FromSystem(string library, Assembly assembly) =>
        !OperatingSystem.IsMacOS() && NativeLibrary.TryLoad(library, assembly, DllImportSearchPath.System32, out var handle)
            ? handle : IntPtr.Zero;

    /// Whether <paramref name="library"/> is one of the shipped ones, by name
    /// with or without its extension ("libSkiaSharp", "libSkiaSharp.so").
    public static bool IsBeside(string library, IEnumerable<string> beside)
    {
        var name = Path.GetFileName(library);
        return beside.Any(b => name.StartsWith(Stem(b), StringComparison.OrdinalIgnoreCase));
    }

    private static string Stem(string file)
    {
        var name = Path.GetFileName(file);
        foreach (var ext in (string[])[".dll", ".so", ".dylib"])
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return name[..^ext.Length];
        return name;
    }
}
