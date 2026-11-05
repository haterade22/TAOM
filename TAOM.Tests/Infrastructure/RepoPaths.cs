using System.Collections.Generic;
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

    /// <summary>The compiled code roots a source-scanning gate reads. This line has one; the 1.5.x line also scans its Client submodule, which does not exist here.</summary>
    public static readonly string[] CodeRoots = { "Main" };

    /// <summary>
    /// Every .cs file under the given repo roots (the compiled code roots when none are named), skipping
    /// any path with an obj or bin segment below the repo root.
    /// </summary>
    public static IEnumerable<string> CSharpFiles(params string[] roots)
    {
        var repoDir = RepoPath();
        return (roots.Length == 0 ? CodeRoots : roots)
            .SelectMany(root => Directory.EnumerateFiles(RepoPath(root), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Substring(repoDir.Length).Split('\\', '/').Any(seg => seg == "obj" || seg == "bin"));
    }

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
    /// the rest of that line. Main/SubModule.cs and Main/IoC.cs had no such literal at 1b00d1acd.
    /// </summary>
    public static string StripComments(string source) =>
        CommentPattern.Replace(source, m => Regex.Replace(m.Value, "[^\n]", " "));

    /// <summary>
    /// True when <paramref name="needle"/> appears in the file outside comments (as a whole word when
    /// <paramref name="wholeWord"/>). The file is read raw first and only stripped when the raw text
    /// holds the needle, which is a necessary condition, so a scan over every Main file strips only
    /// its few candidates and returns the same set as stripping them all.
    /// </summary>
    public static bool MentionsOutsideComments(string path, string needle, bool wholeWord = false)
    {
        var text = File.ReadAllText(path);
        if (text.IndexOf(needle, System.StringComparison.Ordinal) < 0)
            return false;

        var stripped = StripComments(text);
        return wholeWord
            ? Regex.IsMatch(stripped, @"\b" + Regex.Escape(needle) + @"\b")
            : stripped.Contains(needle);
    }

    private static string ThisFile([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
