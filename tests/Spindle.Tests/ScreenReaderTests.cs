namespace Spindle.Tests;

/// Whether a screen reader runs (ScreenReader), from what the system said.
public sealed class ScreenReaderTests
{
    [Fact]
    public void Orca_running_is_enough() => Assert.True(ScreenReader.Decide(true, "", ""));

    [Fact]
    public void Gnome_screen_reader_switch_is_enough() => Assert.True(ScreenReader.Decide(false, "true\n", "b false"));

    [Fact]
    public void Accessibility_bus_enabled_is_enough() => Assert.True(ScreenReader.Decide(false, "false", "b true\n"));

    [Theory]
    [InlineData("false", "b false")]
    [InlineData("", "")]                                  // neither tool here
    [InlineData("No such schema", "Failed to get property")]
    public void Otherwise_no(string gsettings, string bus) => Assert.False(ScreenReader.Decide(false, gsettings, bus));

    [Fact]
    public void Never_on_other_systems()
    {
        if (!OperatingSystem.IsLinux()) Assert.False(ScreenReader.Running);
    }
}
