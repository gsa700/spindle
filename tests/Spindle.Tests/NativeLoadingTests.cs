namespace Spindle.Tests;

// Marginal's security review 2026-10-31, C2: system libraries from the
// system's places, the shipped ones from beside the program.
public class NativeLoadingTests
{
    private static readonly string[] Shipped = ["libSkiaSharp.so", "libHarfBuzzSharp.so", "libe_sqlite3.so", "av_libglesv2.dll"];

    [Theory]
    [InlineData("libSkiaSharp", true)]
    [InlineData("libHarfBuzzSharp.so", true)]
    [InlineData("libe_sqlite3", true)]
    [InlineData("av_libglesv2.dll", true)]
    [InlineData("libpulse-simple.so.0", false)]
    [InlineData("libX11.so.6", false)]
    [InlineData("libc", false)]
    [InlineData("dwmapi.dll", false)]
    [InlineData("libFLAC.so.14", false)]
    public void Only_the_shipped_libraries_come_from_beside_the_program(string library, bool beside) =>
        Assert.Equal(beside, NativeLoading.IsBeside(library, Shipped));

    [Fact]
    public void A_library_the_system_lacks_falls_back_to_the_default_search() =>
        Assert.Equal(IntPtr.Zero, NativeLoading.Resolve("libspindle-no-such-library.so.9", typeof(NativeLoadingTests).Assembly, null));

    [Fact]
    public void A_system_library_comes_from_the_system()
    {
        if (OperatingSystem.IsMacOS()) return;   // the Mac is left to library validation
        var name = OperatingSystem.IsWindows() ? "kernel32.dll" : "libc.so.6";
        if (!OperatingSystem.IsWindows() && !File.Exists("/lib/x86_64-linux-gnu/libc.so.6") && !File.Exists("/lib64/libc.so.6")
            && !File.Exists("/lib/aarch64-linux-gnu/libc.so.6") && !File.Exists("/usr/lib/libc.so.6")) return;
        Assert.NotEqual(IntPtr.Zero, NativeLoading.FromSystem(name, typeof(NativeLoadingTests).Assembly));
    }

    [Fact]
    public void A_mac_leaves_it_to_library_validation()
    {
        if (!OperatingSystem.IsMacOS()) return;
        Assert.Equal(IntPtr.Zero, NativeLoading.FromSystem("libSystem.B.dylib", typeof(NativeLoadingTests).Assembly));
    }
}
