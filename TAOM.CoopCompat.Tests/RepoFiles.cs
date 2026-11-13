using System.IO;
using System.Runtime.CompilerServices;

namespace TAOM.CoopCompat.Tests;

internal static class RepoFiles
{
    // <repo>/TAOM.CoopCompat.Tests/RepoFiles.cs -> <repo>
    public static string Path(string relative, [CallerFilePath] string thisFile = "")
    {
        var repoRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(thisFile, "..", ".."));
        return System.IO.Path.Combine(repoRoot, relative);
    }

    public static string Read(string relative) => File.ReadAllText(Path(relative));
}
