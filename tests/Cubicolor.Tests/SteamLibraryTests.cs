using System;
using System.IO;
using System.Runtime.InteropServices;
using Cubicolor.Platform;
using Xunit;

namespace Cubicolor.Tests;

// The Steam library of the SDK in packaging/steam/sdk (not in the repository; docs/steam.md). Nothing is started:
// that would show whoever runs the tests as playing the game.
public sealed class SteamLibraryTests
{
    [Fact]
    public void The_sdks_library_has_every_function_the_game_calls()
    {
        string redistributable = Path.Combine(Repo.Root, "packaging", "steam", "sdk", "redistributable_bin");
        string path = Path.Combine(redistributable, OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsWindows() ? "win64" : "linux64", Steam.LibraryName);
        Assert.SkipUnless(File.Exists(path), "no SDK in packaging/steam/sdk");

        IntPtr library = NativeLibrary.Load(path);
        try
        {
            Assert.Empty(Steam.Missing(library));
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }
}
