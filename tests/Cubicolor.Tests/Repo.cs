using System;
using System.IO;

namespace Cubicolor.Tests;

// Paths into the repository the tests run from.
internal static class Repo
{
    public static readonly string Root = FindRoot();

    public static string Assets(string relative) => Path.Combine(Root, "assets", relative);

    public static string Fixture(string relative) => Path.Combine(AppContext.BaseDirectory, "Fixtures", relative);

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Cubicolor.slnx")))
                return dir.FullName;
        throw new InvalidOperationException("The tests run outside the repository");
    }
}
