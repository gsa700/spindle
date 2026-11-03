using Spindle.Install;

namespace Spindle.Tests;

/// From Marginal's InstallSecurityTests (its 2026-10-31 review), with the code.
public class InstallCommandLineTests
{
    [Theory]
    [InlineData(new[] { "install" }, InstallAction.None, false)]
    [InlineData(new[] { "Uninstall", "quiet" }, InstallAction.None, false)]
    [InlineData(new[] { "/Users/me/Music/install" }, InstallAction.None, false)]
    [InlineData(new[] { "--install" }, InstallAction.Install, false)]
    [InlineData(new[] { "-Install", "--quiet" }, InstallAction.Install, true)]
    [InlineData(new[] { "--uninstall", "--quiet" }, InstallAction.Uninstall, true)]
    public void Only_dashed_words_are_switches(string[] args, InstallAction action, bool quiet)
    {
        var request = InstallCommandLine.Parse(args);
        Assert.Equal(action, request.Action);
        Assert.Equal(quiet, request.Quiet);
    }

    [Fact]
    public void A_slash_is_a_switch_only_on_windows() =>
        Assert.Equal(OperatingSystem.IsWindows() ? InstallAction.Uninstall : InstallAction.None,
            InstallCommandLine.Parse(["/uninstall"]).Action);

    [Fact]
    public void A_staged_program_must_say_its_version()
    {
        var file = Path.Combine(Path.GetTempPath(), $"spindle-says-{Guid.NewGuid():N}");
        File.WriteAllText(file, "...InformationalVersion 0.2.0+abcdef0...");
        try
        {
            Assert.True(UpdateService.SaysVersion(file, "v0.2.0"));
            Assert.False(UpdateService.SaysVersion(file, "v0.3.0"));
        }
        finally { File.Delete(file); }
    }
}
