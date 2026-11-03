// Spindle — the running program's own version.
//
// From Deadwax (src/Deadwax.Metadata/AppVersion.cs; Marginal had a copy),
// moved here 2026-11-02. The version is the ENTRY assembly's, the app's or
// the CLI's: Spindle's own projects carry no app version. Only with no entry
// assembly (some test hosts) does it fall back to this assembly's.
using System.Reflection;

namespace Spindle;

/// The running program's own version (the app, or the CLI), without the
/// source-revision suffix: what the .log's first line says, and what an app
/// tells MusicBrainz and the Cover Art Archive it is. In Deadwax both were
/// constants, "0.1", that nothing ever bumped (fixed 2026-10-10 and -11).
public static class AppVersion
{
    public static string Current
    {
        get
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly;
            var v = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            var plus = v.IndexOf('+');
            return plus >= 0 ? v[..plus] : v;
        }
    }
}
