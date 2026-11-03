// Spindle — what to send when something goes wrong.
//
// From the apps (written in AlbumWall 2026-11-01 and copied to Deadwax and
// Marginal unchanged), moved here 2026-11-02. What was each app's own is now
// handed in by CatchCrashes: its name, its version, and on Linux how its
// window is drawn. The UI thread's handler stays in each app (one line, in
// App.OnFrameworkInitializationCompleted), since Spindle has no UI in it:
//
//     Dispatcher.UIThread.UnhandledException += (_, e) => Diagnostics.Record(e.Exception);
//
// Three pieces (his pick, 2026-11-01: "start on the diagnostics"):
//  - the log, LogFile: the console teed to <app>.log beside the settings;
//  - the crash catcher, here: an exception nothing caught is written to
//    <app>-crash.txt with the end of the log, so it outlives the two runs
//    the log keeps, and the next start says it is there;
//  - the report, here: "Copy diagnostics" on the About page puts the version,
//    the system, the last crash and the end of the log on the clipboard,
//    ready to paste into an issue. Nothing is ever sent anywhere by the app.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Spindle;

public static class Diagnostics
{
    private static string _name = "app", _version = "";
    private static Func<string>? _display;

    private static string Folder => Path.GetDirectoryName(LogFile.Path) ?? "";

    /// One file, replaced by each crash: the latest is the one worth reading.
    public static string CrashPath => Path.Combine(Folder, _name.ToLowerInvariant() + "-crash.txt");

    /// When the last crash report was written, or null if there is none.
    public static DateTime? LastCrash => File.Exists(CrashPath) ? File.GetLastWriteTime(CrashPath) : null;

    /// Right after LogFile.Start, before anything else can throw. `name` is
    /// the app's ("Deadwax"), `version` what it reports, `display` (Linux only,
    /// read when a report is made) how its window is drawn ("native Wayland").
    public static void CatchCrashes(string name, string version, Func<string>? display = null)
    {
        (_name, _version, _display) = (name, version, display);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Record(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));

        // A task whose failure nobody awaited. It does not close the app (it has
        // not since .NET 4.5), but it was a failure all the same, and it used to
        // vanish without a word.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Console.Error.WriteLine($"[error] a background task failed: {e.Exception}");
            e.SetObserved();
        };

        if (LastCrash is { } when)
            Console.WriteLine($"[log] the last crash report is from {when:yyyy-MM-dd HH:mm}: {CrashPath}");
    }


    private static Exception? _recorded;

    /// Best effort and never throws: it runs while the app is going down.
    public static void Record(Exception ex)
    {
        // The UI thread's handler and the AppDomain's can both see one exception.
        if (ReferenceEquals(ex, _recorded)) return;
        _recorded = ex;
        try { Console.Error.WriteLine($"[crash] {ex}"); } catch { }
        try
        {
            var text = new StringBuilder()
                .AppendLine($"{_name} {_version} closed unexpectedly at {DateTime.Now:yyyy-MM-dd HH:mm:ss}.")
                .AppendLine()
                .AppendLine(ex.ToString())
                .AppendLine()
                .AppendLine("--- the end of the log ---")
                .AppendLine(LogTail(100));
            File.WriteAllText(CrashPath, text.ToString());
        }
        catch { }
    }

    /// The text "Copy diagnostics" puts on the clipboard.
    public static string Report()
    {
        var text = new StringBuilder();
        text.AppendLine($"{_name} {_version}");
        text.AppendLine($"System: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        text.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}, "
                      + (RuntimeFeature.IsDynamicCodeSupported ? "JIT" : "Native AOT")
                      + $", {RuntimeInformation.ProcessArchitecture} process");
        if (OperatingSystem.IsLinux())
            text.AppendLine($"Desktop: {Env("XDG_CURRENT_DESKTOP")}, session {Env("XDG_SESSION_TYPE")}, "
                          + (_display?.Invoke() ?? "(not said)"));
        text.AppendLine($"Program: {Environment.ProcessPath}");
        text.AppendLine($"Log: {LogFile.Path}");
        text.AppendLine($"Report made: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        if (LastCrash is not null)
        {
            text.AppendLine().AppendLine("--- the last crash ---");
            try { text.AppendLine(File.ReadAllText(CrashPath).TrimEnd()); } catch (Exception ex) { text.AppendLine(ex.Message); }
        }
        text.AppendLine().AppendLine("--- the end of the log ---").AppendLine(LogTail(300));
        return text.ToString();

        static string Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : "(unset)";
    }

    /// The last lines of this run's log. Read shared: LogFile still has it open.
    private static string LogTail(int lines)
    {
        try
        {
            using var stream = new FileStream(LogFile.Path, FileMode.Open, FileAccess.Read,
                                              FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var all = reader.ReadToEnd().Split('\n');
            return string.Join('\n', all.Skip(Math.Max(0, all.Length - lines))).TrimEnd();
        }
        catch (Exception ex)
        {
            return $"(no log: {ex.Message})";
        }
    }
}
