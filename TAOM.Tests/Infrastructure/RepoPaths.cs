using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Locates a repo file from THIS source file's compile-time path, so a test that reads shipped
/// source or ModuleData does not depend on the test assembly's output layout (bin/Debug/net472
/// depth) staying what it is today. Import with <c>using static TAOM.Tests.Infrastructure.RepoPaths;</c>.
/// </summary>
public static class RepoPaths
{
    private static readonly Regex CommentPattern =
        new(@"/\*.*?\*/|//[^\n]*", RegexOptions.Compiled | RegexOptions.Singleline);

    public static string RepoPath(params string[] parts)
    {
        // TWO levels: <repo>/TAOM.Tests/Infrastructure -> TAOM.Tests -> <repo>.
        var repoRoot = Path.GetFullPath(Path.Combine(ThisFile(), "..", ".."));
        return Path.Combine(new[] { repoRoot }.Concat(parts).ToArray());
    }

    /// <summary>
    /// The text of a repo file for a source-scanning test, CRLF normalised to LF. A missing file FAILS
    /// the test: a wiring gate that goes Inconclusive when it cannot find its file silently stops
    /// guarding. With <paramref name="stripComments"/>, comments are blanked (see
    /// <see cref="StripComments"/>), so a commented-out line can no longer satisfy a "this is wired"
    /// assertion.
    /// </summary>
    /// <param name="relativePath">Repo-relative, either separator, for example "Main/SubModule.cs".</param>
    public static string ReadSource(string relativePath, bool stripComments = false)
    {
        var path = RepoPath(relativePath.Split('/', '\\'));
        if (!File.Exists(path))
            Assert.Fail($"Source file not found: {path}. RepoPaths resolves from its own compile-time path "
                + "([CallerFilePath]); a <PathMap> on TAOM.Tests would break that.");

        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        return stripComments ? StripComments(text) : text;
    }

    /// <summary>
    /// Blanks // and /* */ comments to spaces, keeping length and line breaks so IndexOf offsets and
    /// line numbers still line up. Not string-literal aware: a "//" inside a string literal also blanks
    /// the rest of that line. Main/SubModule.cs and Main/IoC.cs had no such literal at 4c728dac.
    /// </summary>
    public static string StripComments(string source) =>
        CommentPattern.Replace(source, m => Regex.Replace(m.Value, "[^\n]", " "));

    private static string ThisFile([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
