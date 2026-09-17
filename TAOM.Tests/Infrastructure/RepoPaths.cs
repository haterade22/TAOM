using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Locates a repo file from THIS source file's compile-time path, so a test that reads shipped
/// source or ModuleData does not depend on the test assembly's output layout (bin/Debug/net472
/// depth) staying what it is today. Import with <c>using static TAOM.Tests.Infrastructure.RepoPaths;</c>.
/// </summary>
public static class RepoPaths
{
    public static string RepoPath(params string[] parts)
    {
        // TWO levels: <repo>/TAOM.Tests/Infrastructure -> TAOM.Tests -> <repo>.
        var repoRoot = Path.GetFullPath(Path.Combine(ThisFile(), "..", ".."));
        return Path.Combine(new[] { repoRoot }.Concat(parts).ToArray());
    }

    private static string ThisFile([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
