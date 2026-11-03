namespace Spindle.Tests;

/// The log and the crash report, as an app starts them. Both change the
/// process's console and its crash handlers, so they share one class (xUnit
/// runs a class's tests one at a time) and put the console back after.
public sealed class DiagnosticsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "spindle-diag-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly TextWriter _out = Console.Out, _err = Console.Error;

    public void Dispose()
    {
        // Back to the console the runner gave us; LogFile's own memory of the
        // first console is that same one.
        Console.SetOut(_out);
        Console.SetError(_err);
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string Log => Path.Combine(_dir, "testapp.log");

    [Fact]
    public void The_console_goes_to_the_log_too_and_the_last_run_is_kept()
    {
        LogFile.Start(_dir, "testapp.log");
        Assert.Equal(Log, LogFile.Path);
        Console.WriteLine("first run says this");
        Console.Error.WriteLine("and this went wrong");

        LogFile.Start(_dir, "testapp.log");
        Console.WriteLine("second run");

        var last = File.ReadAllText(Log + ".1");
        Assert.Contains("[log] ", last);
        Assert.Contains("first run says this", last);
        Assert.Contains("and this went wrong", last);          // errors too
        Assert.Matches(@"\n\s+\d+\.\d{3}  first run says this", last);   // stamped with the time since start
        Assert.Contains("second run", ReadShared(Log));
        Assert.DoesNotContain("second run", last);
    }

    [Fact]
    public void A_crash_is_written_with_the_apps_name_and_the_end_of_the_log()
    {
        LogFile.Start(_dir, "testapp.log");
        Diagnostics.CatchCrashes("TestApp", "9.8.7", () => "native Wayland");
        Console.WriteLine("the line before it went down");

        Diagnostics.Record(new InvalidOperationException("nothing caught this"));

        Assert.Equal(Path.Combine(_dir, "testapp-crash.txt"), Diagnostics.CrashPath);
        var crash = File.ReadAllText(Diagnostics.CrashPath);
        Assert.StartsWith("TestApp 9.8.7 closed unexpectedly at ", crash);
        Assert.Contains("InvalidOperationException: nothing caught this", crash);
        Assert.Contains("the line before it went down", crash);
        Assert.NotNull(Diagnostics.LastCrash);
    }

    [Fact]
    public void The_report_says_what_to_say_and_names_no_one_else()
    {
        LogFile.Start(_dir, "testapp.log");
        Diagnostics.CatchCrashes("TestApp", "9.8.7", () => "native Wayland");
        Console.WriteLine("something worth reading");

        var report = Diagnostics.Report();
        Assert.StartsWith("TestApp 9.8.7", report);
        Assert.Contains("System: ", report);
        Assert.Contains(System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported ? "JIT" : "Native AOT", report);
        Assert.Contains($"Log: {Log}", report);
        Assert.Contains("something worth reading", report);
        if (OperatingSystem.IsLinux()) Assert.Contains("native Wayland", report);
        Assert.DoesNotContain("AlbumWall", report);
        Assert.DoesNotContain("Deadwax", report);
    }

    private static string ReadShared(string path)
    {
        using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return new StreamReader(s).ReadToEnd();
    }
}
