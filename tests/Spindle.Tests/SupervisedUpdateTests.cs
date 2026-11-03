using Spindle.Install;

namespace Spindle.Tests;

/// A supervised update (a kiosk's launcher restarts the app): the app puts the
/// new build in place itself before it exits, the old one kept in previous/
/// for the launcher to put back (UpdateService.InstallBeside, from AlbumWall).
public sealed class SupervisedUpdateTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("spindle-supervised-").FullName;
    private string From => Path.Combine(_root, "new");
    private string To => Path.Combine(_root, "installed");

    public SupervisedUpdateTests()
    {
        Directory.CreateDirectory(From);
        Directory.CreateDirectory(To);
        File.WriteAllText(Path.Combine(From, "TestApp"), "new program");
        File.WriteAllText(Path.Combine(From, "libSkiaSharp.so"), "new lib");
        File.WriteAllText(Path.Combine(From, "libe_sqlite3.so"), "new sqlite");
        File.WriteAllText(Path.Combine(To, "TestApp"), "old program");
        File.WriteAllText(Path.Combine(To, "libSkiaSharp.so"), "old lib");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private static string[] Names => ["libSkiaSharp.so", "libe_sqlite3.so", "TestApp"];   // the program last

    [Fact]
    public void The_new_build_goes_in_and_the_old_one_is_kept_in_previous()
    {
        UpdateService.InstallBeside(From, To, Names, "TestApp");

        Assert.Equal("new program", File.ReadAllText(Path.Combine(To, "TestApp")));
        Assert.Equal("new lib", File.ReadAllText(Path.Combine(To, "libSkiaSharp.so")));
        Assert.Equal("new sqlite", File.ReadAllText(Path.Combine(To, "libe_sqlite3.so")));
        var previous = Path.Combine(To, UpdateService.PreviousFolder);
        Assert.Equal("old program", File.ReadAllText(Path.Combine(previous, "TestApp")));
        Assert.Equal("old lib", File.ReadAllText(Path.Combine(previous, "libSkiaSharp.so")));
        Assert.True(File.Exists(Path.Combine(previous, "libe_sqlite3.so.absent")));     // putting the old build back removes it
        Assert.Empty(Directory.GetFiles(To, ".*.new"));
        if (!OperatingSystem.IsWindows())
            Assert.True(File.GetUnixFileMode(Path.Combine(To, "TestApp")).HasFlag(UnixFileMode.UserExecute));
    }

    [Fact]
    public void A_build_that_cannot_be_put_in_leaves_the_old_one_running()
    {
        File.Delete(Path.Combine(From, "TestApp"));      // the program missing from the staged build
        Assert.ThrowsAny<IOException>(() => UpdateService.InstallBeside(From, To, Names, "TestApp"));

        Assert.Equal("old program", File.ReadAllText(Path.Combine(To, "TestApp")));
        Assert.Equal("old lib", File.ReadAllText(Path.Combine(To, "libSkiaSharp.so")));
        Assert.False(File.Exists(Path.Combine(To, "libe_sqlite3.so")));                // was not there before
        Assert.Empty(Directory.GetFiles(To, ".*.new"));
    }

    [Fact]
    public void Supervision_is_the_apps_own_variable()
    {
        Assert.Equal("TESTAPP_SUPERVISED", AppInfo.Current.SupervisedVariable);
        var was = Environment.GetEnvironmentVariable("TESTAPP_SUPERVISED");
        try
        {
            Environment.SetEnvironmentVariable("TESTAPP_SUPERVISED", "1");
            Assert.True(UpdateService.Supervised);
            Environment.SetEnvironmentVariable("TESTAPP_SUPERVISED", null);
            Assert.False(UpdateService.Supervised);
        }
        finally { Environment.SetEnvironmentVariable("TESTAPP_SUPERVISED", was); }
    }

    [Fact]
    public void The_tidy_helper_swaps_nothing_and_starts_nothing()
    {
        Assert.DoesNotContain("mv ", UpdateService.SupervisedScript);
        Assert.DoesNotContain("open", UpdateService.SupervisedScript);
        Assert.Contains("while kill -0 \"$pid\"", UpdateService.SupervisedScript);
    }
}
