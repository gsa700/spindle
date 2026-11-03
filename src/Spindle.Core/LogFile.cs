// Spindle — a copy of the console in a file.
//
// From the apps (AlbumWall's, copied to Deadwax and Marginal 2026-11-01), moved
// here 2026-11-02; the app now says where (Start). Why it exists: a GUI
// program started from the Finder, the Start Menu or a desktop launcher has no
// terminal, so whatever it printed went nowhere, and the one run that
// misbehaved was always the one nobody was recording. Now it goes to
// <app>.log beside the app's settings, and the previous run is kept as
// <app>.log.1.
using System.Text;

namespace Spindle;

public static class LogFile
{
    /// Where this run's log is: set by Start ("" before it).
    public static string Path { get; private set; } = "";

    private static (TextWriter Out, TextWriter Error)? _console;

    /// `folder` is the app's settings folder, `name` its log ("deadwax.log").
    /// Best effort. A log that cannot be opened must never stop the app
    /// starting; it just goes back to being console-only.
    public static void Start(string folder, string name)
    {
        Path = System.IO.Path.Combine(folder, name);
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            if (File.Exists(Path)) File.Move(Path, Path + ".1", overwrite: true);

            var file = new StreamWriter(new FileStream(Path, FileMode.Create, FileAccess.Write,
                                                       FileShare.ReadWrite | FileShare.Delete))
            { AutoFlush = true };

            // Errors too (2026-11-01): Console.Error went only to the terminal,
            // so the line that said what failed was the one line not kept.
            // Around the console as it was before any Start, so a second Start
            // (a test, a restart in one process) tees once, not twice.
            _console ??= (Console.Out, Console.Error);
            Console.SetOut(new Tee(_console.Value.Out, file));
            Console.SetError(new Tee(_console.Value.Error, file));
            Console.WriteLine($"[log] {DateTime.Now:yyyy-MM-dd HH:mm:ss}  pid {Environment.ProcessId}  "
                            + $"{AppVersion.Current}");
        }
        catch { /* console only */ }
    }

    /// Lines are stamped with the time since start, because nearly every
    /// question asked of this log is "in what order, and how far apart?".
    /// Output and Error each have one, writing to the same file, so they share
    /// the clock and the lock.
    private sealed class Tee(TextWriter console, TextWriter file) : TextWriter
    {
        private static readonly System.Diagnostics.Stopwatch _since = System.Diagnostics.Stopwatch.StartNew();
        private static readonly object _gate = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value)
        {
            lock (_gate)
            {
                try { console.WriteLine(value); } catch { /* no console: fine */ }
                try { file.WriteLine($"{_since.Elapsed.TotalSeconds,8:0.000}  {value}"); } catch { }
            }
        }

        public override void Write(char value)
        {
            lock (_gate)
            {
                try { console.Write(value); } catch { }
                try { file.Write(value); } catch { }
            }
        }
    }
}
