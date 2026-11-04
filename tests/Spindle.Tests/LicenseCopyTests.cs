using Spindle.Install;

namespace Spindle.Tests;

/// What the licenses ask to travel with the program reaches the installed copy
/// (InstallLayout.CopyLicenses), on install and on update (2026-11-03: before,
/// no installed copy had them).
public sealed class LicenseCopyTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("spindle-licenses-").FullName;
    private string From => Path.Combine(_root, "from");
    private string To => Path.Combine(_root, "to");

    public LicenseCopyTests()
    {
        Directory.CreateDirectory(Path.Combine(From, "licenses"));
        Directory.CreateDirectory(To);
        File.WriteAllText(Path.Combine(From, "LICENSE"), "GPL v3");
        File.WriteAllText(Path.Combine(From, "THIRD-PARTY-NOTICES.md"), "notices");
        File.WriteAllText(Path.Combine(From, "licenses", "skia-LICENSE.txt"), "BSD");
        File.WriteAllText(Path.Combine(From, "licenses", "README.md"), "what these are");
        File.WriteAllText(Path.Combine(From, "program"), "not a license");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Copies_the_license_the_notices_and_the_texts()
    {
        var copied = InstallLayout.CopyLicenses(From, To);
        Assert.Equal(["LICENSE", "THIRD-PARTY-NOTICES.md", "licenses/ (2)"], copied);
        Assert.Equal("GPL v3", File.ReadAllText(Path.Combine(To, "LICENSE")));
        Assert.Equal("BSD", File.ReadAllText(Path.Combine(To, "licenses", "skia-LICENSE.txt")));
        Assert.False(File.Exists(Path.Combine(To, "program")));   // nothing else
        Assert.Empty(Directory.GetFiles(To, ".*.new", SearchOption.AllDirectories));
    }

    [Fact]
    public void An_update_replaces_texts_and_drops_one_no_longer_shipped()
    {
        Directory.CreateDirectory(Path.Combine(To, "licenses"));
        File.WriteAllText(Path.Combine(To, "LICENSE"), "old");
        File.WriteAllText(Path.Combine(To, "licenses", "gone-LICENSE.txt"), "a library no longer used");
        InstallLayout.CopyLicenses(From, To);
        Assert.Equal("GPL v3", File.ReadAllText(Path.Combine(To, "LICENSE")));
        Assert.False(File.Exists(Path.Combine(To, "licenses", "gone-LICENSE.txt")));
        Assert.True(File.Exists(Path.Combine(To, "licenses", "README.md")));
    }

    [Fact]
    public void A_build_without_them_copies_nothing_and_leaves_what_is_there()
    {
        var bare = Path.Combine(_root, "bare");
        Directory.CreateDirectory(bare);
        File.WriteAllText(Path.Combine(To, "LICENSE"), "kept");
        Assert.Empty(InstallLayout.CopyLicenses(bare, To));
        Assert.Equal("kept", File.ReadAllText(Path.Combine(To, "LICENSE")));
    }

    [Fact]
    public void Into_itself_is_nothing()
    {
        Assert.Empty(InstallLayout.CopyLicenses(From, From));
        Assert.Equal("GPL v3", File.ReadAllText(Path.Combine(From, "LICENSE")));
    }

    [Fact]
    public void A_link_is_not_followed()
    {
        if (OperatingSystem.IsWindows()) return;   // symlinks need rights there
        var secret = Path.Combine(_root, "secret.txt");
        File.WriteAllText(secret, "not for copying");
        File.Delete(Path.Combine(From, "LICENSE"));
        File.CreateSymbolicLink(Path.Combine(From, "LICENSE"), secret);
        File.CreateSymbolicLink(Path.Combine(From, "licenses", "link.txt"), secret);
        InstallLayout.CopyLicenses(From, To);
        Assert.False(File.Exists(Path.Combine(To, "LICENSE")));
        Assert.False(File.Exists(Path.Combine(To, "licenses", "link.txt")));
    }
}
