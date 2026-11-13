using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Pins <c>RepoPaths.ReadSource</c>, the one reader every test that scans Main/SubModule.cs or
/// Main/IoC.cs goes through: LF line endings, either path separator, a missing file FAILS (a wiring
/// gate that goes Inconclusive stops guarding without saying so), and the comment-stripped view keeps
/// length and line breaks while hiding commented-out code.
/// </summary>
[TestClass]
public class RepoPathsTests
{
    [TestMethod]
    public void CSharpFiles_Client_ContainsTheSubModuleAndNothingUnderObjOrBin()
    {
        var files = RepoPaths.CSharpFiles("Client").ToList();

        Assert.IsTrue(files.Any(f => Path.GetFileName(f) == "ClientSubModule.cs"), "ClientSubModule.cs is missing.");
        var repoDir = RepoPaths.RepoPath();
        var built = files.Where(f => f.Substring(repoDir.Length).Split('\\', '/').Any(s => s == "obj" || s == "bin")).ToList();
        CollectionAssert.AreEqual(new string[0], built, "CSharpFiles must skip obj and bin.");
    }

    [TestMethod]
    public void CSharpFiles_WithNoRoots_ReadsEveryCodeRoot()
    {
        CollectionAssert.AreEqual(new[] { "Main", "Client" }, RepoPaths.CodeRoots);
        Assert.IsTrue(RepoPaths.CSharpFiles().Any(f => Path.GetFileName(f) == "ClientSubModule.cs"));
        Assert.IsTrue(RepoPaths.CSharpFiles().Any(f => Path.GetFileName(f) == "SubModule.cs"));
    }

    [TestMethod]
    public void ReadSource_ReturnsTheFileWithLfLineEndingsOnly()
    {
        var text = RepoPaths.ReadSource("TAOM.Tests/Infrastructure/RepoPaths.cs");

        StringAssert.Contains(text, "public static class RepoPaths");
        Assert.IsFalse(text.Contains("\r"), "ReadSource must normalise CRLF to LF.");
    }

    // The repo-file read above passes without normalisation on an LF checkout (Codex review of plan
    // 018, P3), so this one reads a file that certainly holds CRLF. bin/ is ignored by git.
    [TestMethod]
    public void ReadSource_NormalisesAnExplicitCrlfFile_ToLf()
    {
        var probe = RepoPaths.RepoPath("TAOM.Tests", "bin", "repopaths-crlf-probe.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(probe)!);
        File.WriteAllText(probe, "a\r\nb\r\n");
        try
        {
            Assert.AreEqual("a\nb\n", RepoPaths.ReadSource("TAOM.Tests/bin/repopaths-crlf-probe.txt"));
        }
        finally
        {
            File.Delete(probe);
        }
    }

    [TestMethod]
    public void ReadSource_AcceptsEitherPathSeparator()
    {
        Assert.AreEqual(
            RepoPaths.ReadSource("TAOM.Tests/Infrastructure/RepoPaths.cs"),
            RepoPaths.ReadSource(@"TAOM.Tests\Infrastructure\RepoPaths.cs"));
    }

    [TestMethod]
    public void ReadSource_MissingFile_FailsTheTestInsteadOfSkippingIt()
    {
        Assert.ThrowsException<AssertFailedException>(
            () => RepoPaths.ReadSource("Main/NoSuchFile_Plan018.cs"));
    }

    [TestMethod]
    public void StripComments_BlanksLineAndBlockComments_KeepingLengthAndLineBreaks()
    {
        const string source =
            "keep();\n" +
            "// campaignStarter.AddModel(new ParkedModel());\n" +
            "live(); /* block\n spans */ tail();\n";

        var stripped = RepoPaths.StripComments(source);

        Assert.AreEqual(source.Length, stripped.Length);
        CollectionAssert.AreEqual(
            source.Select((c, i) => c == '\n' ? i : -1).Where(i => i >= 0).ToArray(),
            stripped.Select((c, i) => c == '\n' ? i : -1).Where(i => i >= 0).ToArray());
        StringAssert.Contains(stripped, "keep();");
        StringAssert.Contains(stripped, "live();");
        StringAssert.Contains(stripped, "tail();");
        Assert.IsFalse(stripped.Contains("new ParkedModel("), "A commented-out registration must not survive.");
        Assert.IsFalse(stripped.Contains("block") || stripped.Contains("spans"), "A block comment must not survive.");
    }

    [DataTestMethod]
    [DataRow("// FooBehavior only here\nint x;\n", "FooBehavior", false, false)]
    [DataRow("/* FooBehavior */ int x;\n", "FooBehavior", true, false)]
    [DataRow("new FooBehavior();\n", "FooBehavior", false, true)]
    [DataRow("new FooBehavior();\n", "FooBehavior", true, true)]
    [DataRow("new FooBehaviorExtra();\n", "FooBehavior", true, false)]
    [DataRow("new FooBehaviorExtra();\n", "FooBehavior", false, true)]
    public void MentionsOutsideComments_ReadsCodeNotComments(string content, string needle, bool wholeWord, bool expected)
    {
        var probe = RepoPaths.RepoPath("TAOM.Tests", "bin", "repopaths-mentions-probe.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(probe)!);
        File.WriteAllText(probe, content);
        try
        {
            Assert.AreEqual(expected, RepoPaths.MentionsOutsideComments(probe, needle, wholeWord));
        }
        finally
        {
            File.Delete(probe);
        }
    }

    [TestMethod]
    public void MentionsOutsideComments_MissingFile_Throws()
    {
        var missing = RepoPaths.RepoPath("TAOM.Tests", "bin", "no-such-mentions-probe.txt");

        Assert.ThrowsException<FileNotFoundException>(
            () => RepoPaths.MentionsOutsideComments(missing, "X"));
    }

    [TestMethod]
    public void Text_CodeRootFile_ReturnsOneCachedInstance()
    {
        var path = RepoPaths.RepoPath("Main", "SubModule.cs");

        var first = RepoPaths.Text(path);

        Assert.AreEqual(File.ReadAllText(path), first);
        Assert.AreSame(first, RepoPaths.Text(path), "a code-root .cs file is read once per test run");
    }

    [TestMethod]
    public void Text_EitherSeparator_ReturnsTheSameCachedInstance()
    {
        var path = RepoPaths.RepoPath("Client", "ClientSubModule.cs");

        Assert.AreSame(RepoPaths.Text(path), RepoPaths.Text(path.Replace('\\', '/')));
    }

    // A .cs name under bin: only the root check keeps it out of the cache.
    [TestMethod]
    public void Text_FileOutsideTheCodeRoots_IsReadFreshEveryCall()
    {
        var probe = RepoPaths.RepoPath("TAOM.Tests", "bin", "repopaths-text-probe.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(probe)!);
        try
        {
            File.WriteAllText(probe, "first");
            Assert.AreEqual("first", RepoPaths.Text(probe));
            File.WriteAllText(probe, "second");
            Assert.AreEqual("second", RepoPaths.Text(probe), "only the four code roots are cached");
        }
        finally
        {
            File.Delete(probe);
        }
    }

    [TestMethod]
    public void CSharpFiles_KeepsTheCallersRootOrderAndTheDirectoryWalkOrder()
    {
        var repoDir = RepoPaths.RepoPath();
        foreach (var roots in new[] { new[] { "Main", "Client" }, new[] { "Client", "Main" } })
        {
            var walked = roots
                .SelectMany(r => Directory.EnumerateFiles(RepoPaths.RepoPath(r), "*.cs", SearchOption.AllDirectories))
                .Where(f => !f.Substring(repoDir.Length).Split('\\', '/').Any(s => s == "obj" || s == "bin"))
                .ToList();

            CollectionAssert.AreEqual(walked, RepoPaths.CSharpFiles(roots).ToList(), string.Join(",", roots) + ": first call");
            CollectionAssert.AreEqual(walked, RepoPaths.CSharpFiles(roots).ToList(), string.Join(",", roots) + ": second call");
        }
    }

    [TestMethod]
    public void MentionsOutsideComments_MissingCodeRootFile_Throws()
    {
        var missing = RepoPaths.RepoPath("Main", "NoSuchFile_Plan046.cs");

        Assert.ThrowsException<FileNotFoundException>(() => RepoPaths.MentionsOutsideComments(missing, "X"));
        Assert.ThrowsException<FileNotFoundException>(() => RepoPaths.MentionsOutsideComments(missing, "X"),
            "a failed read must cache nothing");
    }
}
