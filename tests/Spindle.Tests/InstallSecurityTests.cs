using System.Diagnostics;
using System.Reflection;
using Spindle.Install;

namespace Spindle.Tests;

public class InstallSecurityTests
{
    // ---- the Windows helpers' PowerShell (M4, L13) ----------------------------

    private static readonly Type Ps = typeof(Spindle.Install.UpdateService).Assembly.GetType("Spindle.Install.PowerShell")!;
    private static T Call<T>(Type type, string name, params object?[] args) =>
        (T)type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!.Invoke(null, args)!;

    [Fact]
    public void A_ps1_is_written_with_the_utf8_byte_order_mark() =>
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF },
            ((System.Text.Encoding)Ps.GetField("FileEncoding", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!).GetPreamble());

    [Fact]
    public void M4_powershell_is_started_from_System32_by_its_full_path()
    {
        var exe = (string)Ps.GetProperty("Exe", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        Assert.Equal(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"), exe);
        var start = Call<ProcessStartInfo>(Ps, "StartInfo", "x.ps1");
        Assert.Equal(exe, start.FileName);
        Assert.Equal("x.ps1", start.ArgumentList[^1]);
    }

    [Theory]
    [InlineData("Dave's Apps", "'Dave''s Apps'")]
    [InlineData("Sean O\x2019Brien", "'Sean O\x2019\x2019Brien'")]
    [InlineData("\x2018\x201A\x201B", "'\x2018\x2018\x201A\x201A\x201B\x201B'")]
    public void L13_every_kind_of_single_quote_is_doubled(string value, string quoted) =>
        Assert.Equal(quoted, Call<string>(Ps, "Quote", value));

    [Fact]
    public void L13_relaunch_arguments_keep_their_spaces_and_quotes()
    {
        IEnumerable<string> args = ["--plain", "two words", "say \"hi\"", @"C:\dir\", ""];
        Assert.Equal(@"--plain ""two words"" ""say \""hi\"""" C:\dir\ """"", Call<string>(Ps, "CommandLine", args));
    }

    /// Runs the real update helper with PowerShell where one is given
    /// (MARGINAL_TEST_PWSH=/path/to/pwsh): the curly quote in the install path
    /// no longer breaks the script, and one in an argument runs nothing.
    [Fact]
    public void L13_the_update_helper_survives_curly_quotes_under_PowerShell()
    {
        var pwsh = Environment.GetEnvironmentVariable("MARGINAL_TEST_PWSH");
        if (string.IsNullOrEmpty(pwsh)) return;
        var root = Directory.CreateTempSubdirectory("testapp-l13-").FullName;
        try
        {
            var dir = Path.Combine(root, "Dave\x2019s Apps");
            Directory.CreateDirectory(dir);
            var stagedDir = Path.Combine(root, "stage", "ex"); Directory.CreateDirectory(stagedDir);
            File.WriteAllText(Path.Combine(stagedDir, "TestApp.exe"), "new");
            var target = Path.Combine(dir, "TestApp.exe"); File.WriteAllText(target, "old");
            var pwned = Path.Combine(root, "PWNED");
            string[] args = [$"x\x2019; Set-Content -Path \"{pwned}\" -Value injected; #"];
            var script = Call<string>(typeof(Spindle.Install.UpdateService), "WindowsApplyScript",
                999999, stagedDir, dir, "TestApp.exe", Array.Empty<string>(), Path.Combine(root, "failed"), Path.Combine(root, "stage"), null,
                Path.Combine(root, "helper"), args, 1);
            var ps1 = Path.Combine(root, "apply.ps1");
            File.WriteAllText(ps1, script);

            var start = new ProcessStartInfo(pwsh) { ArgumentList = { "-NoProfile", "-NonInteractive", "-File", ps1 }, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(start)!)
            {
                var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                Assert.DoesNotContain("ParserError", output.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
            }
            Assert.Equal("new", File.ReadAllText(target));
            Assert.False(File.Exists(pwned));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ---- the second pass: clean-up (L16, L17) ----------------------------------

    [Fact]
    public void L16_a_tool_in_the_current_folder_is_never_the_one_found()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = Directory.CreateTempSubdirectory("testapp-l16-").FullName;
        var path = Environment.GetEnvironmentVariable("PATH");
        var cwd = Environment.CurrentDirectory;
        try
        {
            // A name no system has: update-desktop-database, used before, is real on Fedora (desktop-file-utils).
            var planted = Path.Combine(dir, "testapp-l16-planted");
            File.WriteAllText(planted, "#!/bin/sh\ntouch PLANTED\n");
            File.SetUnixFileMode(planted, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Environment.CurrentDirectory = dir;
            Environment.SetEnvironmentVariable("PATH", ".:" + ":" + "/usr/bin:/bin");
            Assert.Null(ToolPath.Find("testapp-l16-planted"));
            Assert.StartsWith("/", ToolPath.Find("sh"));   // a real one is still found, by its full path
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", path);
            Environment.CurrentDirectory = cwd;
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void L17_no_menu_entry_is_written_for_a_path_with_a_line_break()
    {
        var quote = typeof(Spindle.Install.UpdateService).Assembly.GetType("Spindle.Install.DesktopEntry")!.GetMethod("QuoteExec")!;
        var e = Assert.Throws<TargetInvocationException>(() => quote.Invoke(null, ["/home/x/.local/share/testapp\nExec=/tmp/evil\n/TestApp"]));
        Assert.Equal("InstallBlockedException", e.InnerException!.GetType().Name);
        Assert.Equal(@"""/home/x/a\\\\b/TestApp""", quote.Invoke(null, [@"/home/x/a\b/TestApp"]));
    }

    [Fact]
    public void L17_uninstall_is_refused_where_there_is_no_installer()
    {
        if (!OperatingSystem.IsMacOS()) return;   // elsewhere this WOULD uninstall
        var install = typeof(Spindle.Install.UpdateService).Assembly.GetType("Spindle.Install.InstallService")!;
        var options = Activator.CreateInstance(typeof(Spindle.Install.UpdateService).Assembly.GetType("Spindle.Install.UninstallOptions")!, [false])!;
        var e = Assert.Throws<TargetInvocationException>(() => install.GetMethod("Uninstall")!.Invoke(null, [options]));
        Assert.Equal("InstallBlockedException", e.InnerException!.GetType().Name);
    }

    // ---- the update feed and its downloads (AlbumWall's review, 2026-10-26, INFO) ----

    [Theory]
    [InlineData("https://api.github.com/repos/gsa700/testapp/releases/latest", true)]
    [InlineData("https://github.com/gsa700/testapp/releases/download/v0.8.2/SHA256SUMS", true)]
    [InlineData("https://release-assets.githubusercontent.com/x", true)]
    [InlineData("http://127.0.0.1:8080/feed", true)]
    [InlineData("http://localhost:8080/feed", true)]
    [InlineData("http://github.com/x", false)]
    [InlineData("https://evil.example/x", false)]
    [InlineData("https://github.com.evil.example/x", false)]
    [InlineData("https://evilgithubusercontent.com/x", false)]
    [InlineData("file:///etc/passwd", false)]
    public void The_updater_fetches_from_GitHub_over_https_or_this_computer_only(string url, bool allowed) =>
        Assert.Equal(allowed, Call<bool>(typeof(Spindle.Install.UpdateService), "MayFetch", new Uri(url)));

    [Fact]
    public void A_feed_override_to_another_host_is_ignored()
    {
        var was = Environment.GetEnvironmentVariable(AppInfo.Current.FeedVariable);
        try
        {
            Environment.SetEnvironmentVariable(AppInfo.Current.FeedVariable, "https://evil.example/feed");
            Assert.StartsWith("https://api.github.com/", Call<string>(typeof(Spindle.Install.UpdateService), "FeedUrl", false));
            Environment.SetEnvironmentVariable(AppInfo.Current.FeedVariable, "http://127.0.0.1:9/feed");
            Assert.Equal("http://127.0.0.1:9/feed", Call<string>(typeof(Spindle.Install.UpdateService), "FeedUrl", false));
        }
        finally { Environment.SetEnvironmentVariable(AppInfo.Current.FeedVariable, was); }
    }

    // ---- the right build for the system (FreeBSD, 2026-11-07) -----------------

    [Theory]
    [InlineData("freebsd", System.Runtime.InteropServices.Architecture.X64, "freebsd-x64")]
    [InlineData("freebsd", System.Runtime.InteropServices.Architecture.Arm64, "freebsd-arm64")]
    [InlineData("linux", System.Runtime.InteropServices.Architecture.X64, "linux-x64")]
    [InlineData("osx", System.Runtime.InteropServices.Architecture.Arm64, "osx-arm64")]
    [InlineData("win", System.Runtime.InteropServices.Architecture.X64, "win-x64")]
    public void Each_system_asks_for_its_own_build(string os, System.Runtime.InteropServices.Architecture arch, string rid) =>
        Assert.Equal(rid, UpdateService.RidFor(os, arch));

    // The first eight bytes of real programs: AlbumWall's Linux release (OSABI 3),
    // Fedora's /usr/bin/true (0), AlbumWall built on FreeBSD 15 (9).
    private static readonly byte[] LinuxGnuElf = [0x7F, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 3];
    private static readonly byte[] LinuxSysvElf = [0x7F, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 0];
    private static readonly byte[] FreeBsdElf = [0x7F, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 9];

    [Fact]
    public void A_Linux_program_is_not_taken_as_a_FreeBSD_one_or_the_other_way_round()
    {
        Assert.True(UpdateService.IsElfFor(FreeBsdElf, freeBsd: true));
        Assert.False(UpdateService.IsElfFor(LinuxGnuElf, freeBsd: true));
        Assert.False(UpdateService.IsElfFor(LinuxSysvElf, freeBsd: true));
        Assert.True(UpdateService.IsElfFor(LinuxGnuElf, freeBsd: false));
        Assert.True(UpdateService.IsElfFor(LinuxSysvElf, freeBsd: false));
        Assert.False(UpdateService.IsElfFor(FreeBsdElf, freeBsd: false));
        Assert.False(UpdateService.IsElfFor("MZ\0\0\0\0\0\0"u8, freeBsd: true));
        Assert.False(UpdateService.IsElfFor(FreeBsdElf.AsSpan(0, 4), freeBsd: true));
    }

    [Fact]
    public void The_libssl_names_are_the_runtimes_with_FreeBSDs_own_after_them()
    {
        Assert.Equal(["libssl.so.3", "libssl.so.1.1", "libssl.so.1.0.2", "libssl.so.1.0.0", "libssl.so.10"],
            UpdateService.TlsNames(freeBsd: false, versionOverride: null));
        var bsd = UpdateService.TlsNames(freeBsd: true, versionOverride: null);
        Assert.Equal(["libssl.so.17", "libssl.so.35", "libssl.so.12", "libssl.so.30", "libssl.so.11", "libssl.so.111"], bsd[5..]);
        Assert.Equal("libssl.so.35", UpdateService.TlsNames(freeBsd: false, versionOverride: "35")[0]);
    }
}
