using Spindle.Install;

namespace Spindle.Tests;

// Release zips unpack into a folder of their own, <App>/ (Marginal's security
// review 2026-10-31, C2; AlbumWall and Deadwax 2026-11-05). The updater finds
// the program below the top and takes its libraries from beside it.
public class TopFolderTests
{
    [Fact]
    public void The_program_is_found_in_the_zips_own_folder()
    {
        var root = Directory.CreateTempSubdirectory("spindle-top-").FullName;
        try
        {
            var dir = Directory.CreateDirectory(Path.Combine(root, "TestApp")).FullName;
            File.WriteAllText(Path.Combine(dir, "TestApp"), "x");
            Directory.CreateDirectory(Path.Combine(dir, "licenses"));
            Assert.Equal(Path.Combine(dir, "TestApp"), UpdateService.FindProgram(root, "TestApp"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void A_program_at_the_top_still_wins()
    {
        var root = Directory.CreateTempSubdirectory("spindle-top-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "TestApp"), "x");
            Directory.CreateDirectory(Path.Combine(root, "TestApp.d"));
            File.WriteAllText(Path.Combine(root, "TestApp.d", "TestApp"), "y");
            Assert.Equal(Path.Combine(root, "TestApp"), UpdateService.FindProgram(root, "TestApp"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
