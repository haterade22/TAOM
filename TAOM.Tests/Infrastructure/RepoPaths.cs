using System;
using System.Collections.Concurrent;
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
/// depth) staying what it is today. The .cs text and file lists under Main, Client, Dependencies and
/// CoopCompat are read once per test run (see <see cref="Text"/> and <see cref="CSharpFiles"/>), so a
/// test that writes a .cs file there sees the run's first read. Import with
/// <c>using static TAOM.Tests.Infrastructure.RepoPaths;</c>.
/// </summary>
public static class RepoPaths
{
    private static readonly Regex CommentPattern =
        new(@"/\*.*?\*/|//[^\n]*", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>The compiled code roots a source-scanning gate reads: core and the client submodule (ADR-013).</summary>
    public static readonly string[] CodeRoots = { "Main", "Client" };

    /// <summary>
    /// The roots whose .cs text a run reads once and keeps. No test writes a .cs file under them; a test that
    /// starts to must read that file with File.ReadAllText, not through <see cref="Text"/>.
    /// </summary>
    private static readonly string[] CorpusRoots = { "Main", "Client", "Dependencies", "CoopCompat" };

    // One list per root, in Directory.EnumerateFiles order, so a caller's root order and the walk order both survive.
    private static readonly ConcurrentDictionary<string, string[]> CorpusFiles = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> CorpusText = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every .cs file under the given repo roots (the compiled code roots when none are named), skipping
    /// any path with an obj or bin segment below the repo root. The list for Main, Client, Dependencies
    /// or CoopCompat is walked once per test run; any other root is walked on every call.
    /// </summary>
    public static IEnumerable<string> CSharpFiles(params string[] roots) =>
        (roots.Length == 0 ? CodeRoots : roots).SelectMany(root =>
            Array.IndexOf(CorpusRoots, root) >= 0
                ? CorpusFiles.GetOrAdd(root, r => WalkCSharpFiles(r).ToArray())
                : WalkCSharpFiles(root));

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
    /// assertion. The text comes from <see cref="Text"/>, so a .cs file under the cached roots is read
    /// once per test run.
    /// </summary>
    /// <param name="relativePath">Repo-relative, either separator, for example "Main/SubModule.cs".</param>
    public static string ReadSource(string relativePath, bool stripComments = false)
    {
        var path = RepoPath(relativePath.Split('/', '\\'));
        if (!File.Exists(path))
            Assert.Fail($"Source file not found: {path}. RepoPaths resolves from its own compile-time path "
                + "([CallerFilePath]); a <PathMap> on TAOM.Tests would break that.");

        var text = Text(path).Replace("\r\n", "\n");
        return stripComments ? StripComments(text) : text;
    }

    /// <summary>
    /// The raw text of a file, decoded as File.ReadAllText decodes it. A .cs file under Main, Client,
    /// Dependencies or CoopCompat (outside obj and bin) is read once per test run and the same string is
    /// returned after; every other path reads the disk on each call. A missing file throws
    /// FileNotFoundException and caches nothing.
    /// </summary>
    public static string Text(string path)
    {
        if (CorpusText.TryGetValue(path, out var cached))
            return cached;

        var full = Path.GetFullPath(path);
        return IsCorpusFile(full) ? CorpusText.GetOrAdd(full, p => File.ReadAllText(p)) : File.ReadAllText(path);
    }

    private static IEnumerable<string> WalkCSharpFiles(string root)
    {
        var repoDir = RepoPath();
        return Directory.EnumerateFiles(RepoPath(root), "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f.Substring(repoDir.Length)));
    }

    private static bool IsBuildOutput(string repoRelative) =>
        repoRelative.Split('\\', '/').Any(seg => seg == "obj" || seg == "bin");

    private static bool IsCorpusFile(string fullPath)
    {
        var repoDir = RepoPath();
        return fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            && fullPath.StartsWith(repoDir, StringComparison.OrdinalIgnoreCase)
            && !IsBuildOutput(fullPath.Substring(repoDir.Length))
            && CorpusRoots.Any(root => fullPath.StartsWith(RepoPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Blanks // and /* */ comments to spaces, keeping length and line breaks so IndexOf offsets and
    /// line numbers still line up. Not string-literal aware: a "//" inside a string literal also blanks
    /// the rest of that line. Main/SubModule.cs and Main/IoC.cs had no such literal at 4c728dac.
    /// </summary>
    public static string StripComments(string source) =>
        CommentPattern.Replace(source, m => Regex.Replace(m.Value, "[^\n]", " "));

    /// <summary>
    /// True when <paramref name="needle"/> appears in the file outside comments (as a whole word when
    /// <paramref name="wholeWord"/>). The file is read raw first and only stripped when the raw text
    /// holds the needle, which is a necessary condition, so a scan over every Main file strips only
    /// its few candidates and returns the same set as stripping them all. The file is read through
    /// <see cref="Text"/>, so a .cs file under the cached roots is read once per test run.
    /// </summary>
    public static bool MentionsOutsideComments(string path, string needle, bool wholeWord = false)
    {
        var text = Text(path);
        if (text.IndexOf(needle, System.StringComparison.Ordinal) < 0)
            return false;

        var stripped = StripComments(text);
        return wholeWord
            ? Regex.IsMatch(stripped, @"\b" + Regex.Escape(needle) + @"\b")
            : stripped.Contains(needle);
    }

    private static string ThisFile([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
