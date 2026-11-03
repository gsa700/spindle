using System.Runtime.CompilerServices;
using Spindle.Install;

namespace Spindle.Tests;

/// The install and update code is told which app it is (AppInfo). Here it is
/// a made-up one, TestApp, so a test that passes proves the code holds no real
/// app's name. Its data folder is a scratch one.
internal static class TestAppInfo
{
    public static readonly string DataDir =
        Path.Combine(Path.GetTempPath(), "spindle-testapp-" + Guid.NewGuid().ToString("N")[..8]);

    [ModuleInitializer]
    internal static void Use() => AppInfo.Use(new AppInfo
    {
        Name = "TestApp",
        Description = "An app that exists only in Spindle's tests",
        DataDir = DataDir,
    });
}
