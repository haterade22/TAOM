using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// ADR-009 ratchet: the count of // and /* */ comment lines in each production .cs file may only fall.
/// A comment stays only on the ADR-009 keep-list, and a raised baseline is the review signal for one.
/// The counts in tools/comment-ratchet-baseline.txt are written by python tools/comment_ratchet.py --regen,
/// which counts with the same rule as <see cref="RepoPaths.CommentLineCount"/>. A count below its baseline
/// also fails, so a sweep lowers the baseline in the same change and the gain cannot be spent later.
/// </summary>
[TestClass]
public class CommentRatchetGateTests
{
    private static readonly string[] ScannedRoots = { "Main", "Client", "CoopCompat", "Dependencies" };

    private const string BaselineFile = "tools/comment-ratchet-baseline.txt";

    private const string Regen = "run python tools/comment_ratchet.py --regen and commit the baseline";

    [TestMethod]
    public void NoProductionFile_GainsOrSilentlyLosesACommentLine()
    {
        var baseline = ReadBaseline(File.ReadAllText(RepoPath(BaselineFile.Split('/'))));
        var repo = RepoPath();
        var failures = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scanned = 0;

        foreach (var file in CSharpFiles(ScannedRoots))
        {
            scanned++;
            var relative = file.Substring(repo.Length).TrimStart('\\', '/').Replace('\\', '/');
            seen.Add(relative);
            var count = CommentLineCount(Text(file));
            var allowed = baseline.TryGetValue(relative, out var known) ? known : 0;
            if (count > allowed)
                failures.Add($"{relative}: {count} comment lines (allowed {allowed}). Delete the comment, or, for an "
                    + "ADR-009 keep-list comment, raise this file's baseline in the same change");
            else if (count < allowed)
                failures.Add($"{relative}: {count} is below its baseline {allowed}; {Regen}");
        }

        foreach (var entry in baseline.Keys.Where(k => !seen.Contains(k)))
            failures.Add($"{entry}: a baseline entry matches no file; {Regen}");

        Assert.IsTrue(scanned > 1000, $"only {scanned} .cs files found under {string.Join(", ", ScannedRoots)}");
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void Baseline_IsNotEmpty()
    {
        var baseline = ReadBaseline(File.ReadAllText(RepoPath(BaselineFile.Split('/'))));

        Assert.IsTrue(baseline.Count > 100, "an empty or truncated baseline would pass every file with no comments");
    }

    [TestMethod]
    [DataRow("int x;\n", 0, "no comment")]
    [DataRow("// one\nint x;\n", 1, "full-line comment")]
    [DataRow("int x; // trailing\n", 1, "trailing comment")]
    [DataRow("/// <summary>Doc.</summary>\nint x;\n", 0, "XML doc comment")]
    [DataRow("//// divider\n", 1, "four slashes are not an XML doc comment")]
    [DataRow("/* a\n b\n c */ int x;\n", 3, "block comment over three lines")]
    [DataRow("int x; /* a */ // b\n", 1, "two comments on one line")]
    [DataRow("// a\r\n// b\r\n", 2, "CRLF counts as LF")]
    [DataRow("", 0, "empty file")]
    public void CommentLineCount_CountsEachLineThatHoldsAComment(string source, int expected, string reason)
    {
        Assert.AreEqual(expected, CommentLineCount(source), reason);
    }

    [TestMethod]
    public void ReadBaseline_KeysIgnoreCaseAndSkipBlankAndHashLines()
    {
        var baseline = ReadBaseline("# header\n\nMain/PreFabs/A.cs\t4\r\nMain/B.cs\t2\n");

        Assert.AreEqual(2, baseline.Count);
        Assert.AreEqual(4, baseline["main/prefabs/a.cs"]);
    }

    [TestMethod]
    public void ReadBaseline_MalformedLine_Throws()
    {
        Assert.ThrowsException<FormatException>(() => ReadBaseline("Main/A.cs 4\n"));
    }

    internal static Dictionary<string, int> ReadBaseline(string text)
    {
        var baseline = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.Length == 0 || raw.StartsWith("#"))
                continue;

            var parts = raw.Split('\t');
            if (parts.Length != 2)
                throw new FormatException($"baseline line is not <path><TAB><count>: {raw}");

            baseline[parts[0]] = int.Parse(parts[1]);
        }

        return baseline;
    }
}
